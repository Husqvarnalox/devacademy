import json

ROLES = ("system", "user", "assistant", "tool")


def render_chat(messages, add_generation_prompt=True):
    """Сообщения -> одна строка по шаблону `<|{role}|>\\n{content}<|end|>\\n` для каждого сообщения."""
    parts = []
    for index, message in enumerate(messages):
        if not isinstance(message, dict) or "role" not in message or "content" not in message:
            raise ValueError(f"сообщение {index}: нужен словарь с ключами role и content")
        role, content = message["role"], message["content"]
        if role not in ROLES:
            raise ValueError(f"сообщение {index}: неизвестная роль {role!r}")
        if role == "system" and index != 0:
            raise ValueError("system допускается только первым сообщением")
        if not isinstance(content, str):
            raise ValueError(f"сообщение {index}: content должен быть строкой")
        if "<|" in content:
            raise ValueError(f"сообщение {index}: content не может содержать служебные токены")
        parts.append(f"<|{role}|>\n{content}<|end|>\n")
    if add_generation_prompt:
        parts.append("<|assistant|>\n")
    return "".join(parts)


def extract_tool_call(text):
    """Первый JSON-объект в тексте, у которого есть строковое "name" и объект "arguments"; иначе None."""
    decoder = json.JSONDecoder()
    for start, char in enumerate(text):
        if char != "{":
            continue
        try:
            value, _ = decoder.raw_decode(text, start)
        except ValueError:
            continue
        if isinstance(value, dict) and isinstance(value.get("name"), str) and isinstance(value.get("arguments"), dict):
            return {"name": value["name"], "arguments": value["arguments"]}
    return None


def _is_type(value, type_name):
    if type_name == "string":
        return isinstance(value, str)
    if type_name == "boolean":
        return isinstance(value, bool)
    if type_name == "integer":
        return isinstance(value, int) and not isinstance(value, bool)
    if type_name == "number":
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    return True


def validate_arguments(args, schema):
    """Список строк-ошибок (пустой список - аргументы корректны)."""
    if not isinstance(args, dict):
        return ["type: arguments must be object"]
    properties = schema.get("properties", {})
    errors = [f"missing: {name}" for name in schema.get("required", []) if name not in args]
    for key in sorted(args):
        value = args[key]
        if key not in properties:
            if schema.get("additionalProperties", True) is False:
                errors.append(f"unknown: {key}")
            continue
        spec = properties[key]
        type_name = spec.get("type")
        if type_name is not None and not _is_type(value, type_name):
            errors.append(f"type: {key} must be {type_name}")
            continue
        if "enum" in spec and value not in spec["enum"]:
            errors.append(f"enum: {key}")
        if isinstance(value, (int, float)) and not isinstance(value, bool):
            if ("minimum" in spec and value < spec["minimum"]) or ("maximum" in spec and value > spec["maximum"]):
                errors.append(f"range: {key}")
    return errors


def _tool_message(name, payload):
    return {"role": "tool", "name": name, "content": json.dumps(payload, ensure_ascii=False)}


def execute_tool_call(call, tools, confirm):
    """Выполнить вызов инструмента и вернуть сообщение роли tool. Исключения наружу не выходят."""
    name = call["name"]
    tool = tools.get(name)
    if tool is None:
        return _tool_message(name, {"ok": False, "error": f"unknown tool: {name}"})
    errors = validate_arguments(call["arguments"], tool["schema"])
    if errors:
        return _tool_message(name, {"ok": False, "error": "invalid arguments", "details": errors})
    if tool.get("dangerous", False):
        try:
            approved = confirm is not None and confirm(call) is True
        except Exception:
            approved = False
        if not approved:
            return _tool_message(name, {"ok": False, "error": "not confirmed"})
    try:
        return _tool_message(name, {"ok": True, "result": tool["fn"](**call["arguments"])})
    except Exception as exc:
        return _tool_message(name, {"ok": False, "error": f"{type(exc).__name__}: {exc}"})
