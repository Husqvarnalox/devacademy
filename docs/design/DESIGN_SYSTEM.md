# DevAcademy design system

Direction: an engineering notebook. Warm paper and ink, one teal accent, typography carries the identity. Dense enough for an IDE, calm enough for a beginner.

## Principles
1. Type first. Hierarchy comes from size, weight and spacing, not from boxes and colors.
2. One accent (teal) for the primary action, selection and progress. Status colors are reserved for status.
3. Flat. 1px lines, no gradients, glass, glow or decorative shadows. Shadows only on floating layers (flyouts, dialogs).
4. Icons are functional (Lucide only): navigation, toolbars, status markers, icon-only buttons. Never decorative.
5. Plain Russian. Explain jargon on first use. Icon-only buttons always have a tooltip and an automation name.

## Tokens
Defined in `App.axaml` (Light and Dark dictionaries). Brush keys: Bg, Surface, SurfaceSunken, Line, LineStrong, Text, TextSecondary, TextMuted, Accent, AccentHover, AccentSubtle, Success, Warn, Danger (+ Subtle variants). Legacy aliases are kept for C# controls.
Text and its tinted backgrounds meet 4.5:1 (checked with a script; see the handoff for the numbers).

Spacing: 4 / 8 / 12 / 16 / 24 / 32 / 48. Radius: 6 controls, 8 surfaces. Heights: 32 default, 36 primary on hub pages, 28 compact in IDE toolbars. Hit targets are at least 28 px. Hub pages: max width 1040, 32 px gutters. Transitions 120 ms.

## Typography
IBM Plex Sans (UI, three weights: Regular, Medium, SemiBold) and IBM Plex Mono (code), embedded under `Assets/Fonts` (OFL texts included).

| Class | Size/line | Use |
|---|---|---|
| display | 28/34 | page title |
| h1 | 22/28 | section page title |
| h2 | 17/24 | section heading |
| h3 | 14/20 | group heading |
| body | 14/22 | default |
| reading | 15/25, about 680 px | lesson theory |
| small | 13/18 | secondary text |
| caption | 12/16 | meta |
| mono | 13/20 | code |
| overline | 11/16, +0.06em, uppercase | labels |

## Components (Styles/Theme.axaml)
- Buttons: `primary` (one per view), default (secondary), `ghost`, `danger`, `compact` (28), `large` (36), `icon`. Hover, pressed, disabled and a visible focus ring are defined.
- Tabs: text with a 2px accent underline. Inputs: 32 px, 1px line, accent focus border.
- Badges `badge` (+accent, success, warn, danger): subtle tinted chip, never more than a few per row.
- Progress: 4px bar. Overline label. Callouts `callout` (+warn, danger, success).
- Rows: `Button.row` in `ItemsControl.rows` inside a `panel`; hover SurfaceSunken, selected AccentSubtle plus a 2px marker.
- Choices: `Button.tile` (+selected), `ListBox.choices`, `ListBox.segmented`, quiz `Border.option` (selected / correct / wrong).
- Icons: `<c:Icon Kind=...>` with Lucide geometries in `Styles/Icons.axaml`, stroked with the inherited foreground.
- Segmented switch: `Border.segmented` holding `Button.segment` (+`active`). For views of the same content (theory section, practice scope, catalog filter), never for moving between screens.
- Tables: `Border.tablehead` (overline cells) over `Button.row.tablerow` / `Border.tablerow` lines; hairlines only, no outer box. `Border.tablerow.failed` is the DangerSubtle band of a failed test.
- Pane chrome: `Border.bar` (44 px headers and toolbars), `Border.footerbar` (pane footer), `GridSplitter.hairline` (+`rows`): a 5 px grab area that draws one 1 px Line, accent on hover.
- Disclosure: `ToggleButton.disclosure` (text + chevron that turns) for optional blocks such as stdin; no Expander box.
- `Button.quiet.active`: a quiet toggle that is on (the AI tutor panel is open).

## Layouts
- Shell: sidebar 220 px (Главная, Обучение, Практика, Проекты; Настройки and collapse at the bottom), an icon rail of 56 px in lessons and narrow windows. Practice is listed when any exercise or placement check exists.
- Lesson workspace: course pane (course, progress, numbered lessons) | lesson (44 px header with the way back to the course and the Theory / Practice / Test tabs; content at most 760 px wide; footer with what completes the lesson, the AI tutor toggle and the next step) | editor (file + Debug / Check / Run, an info strip with language and shortcuts, the editor, then Output / Check / Hints / Problems / Debug). Below 1100 px the course pane starts hidden; below 900 px the editor goes under the lesson. Splitters keep their size while the window is open.
- Theory is shown one section at a time (Просто → Точнее → Примеры → Частые ошибки → Итог → Глубже, as the lesson has them) with a "Дальше" button; "Всё сразу" shows the whole page and is remembered (`ui.theory.all`).
- Hub pages (Главная, Практика): a main column and a 320–340 px side column that moves under the main one below 980 px (`Views/Responsive.cs`).
- Practice: algorithm problems or every course exercise (segmented), filtered by title, language, difficulty and state; placement checks ("Проверка знаний") and solved counts per course in the side column. Only real data: solved = all tests passed, credited = a placement quiz was passed. No skill levels or scores.

## Editor
Editor background is Surface (Light `#FFFFFF`, Dark `#1E1D1A`), current line SurfaceSunken, gutter TextMuted, caret Text, selection `SelectionBrush` (Light `#D8E9E6`, Dark `#1F3532`), matching brackets a subtle outline. Syntax colours come from two embedded TextMate themes (`Assets/Themes/devacademy-{light,dark}.json`, loaded by `Controls/SyntaxThemes.cs`, switched at runtime with the app theme). Read-only code blocks use the same highlighter.

| Role | Light | Dark |
| --- | --- | --- |
| text / parameter | `#1D1C1A` (17.0) | `#ECE9E3` (13.9) |
| comment (italic) | `#6B665D` (5.7) | `#9D988E` (5.9) |
| keyword, tag | `#0B5F8A` (7.0) | `#7FB4D8` (7.6) |
| string | `#2A7239` (5.9) | `#9CCB8A` (9.1) |
| number | `#9A4A00` (6.3) | `#E0A764` (7.9) |
| function, attribute | `#6B4E16` (7.7) | `#E6D49A` (11.4) |
| type | `#0E6A64` (6.4) | `#6CC6BD` (8.4) |
| constant.language | `#A4373A` (6.6) | `#E8908A` (7.1) |
| punctuation | `#57534C` (7.6) | `#B7B2A8` (8.0) |
| invalid (underline) | `#B3261E` (6.5) | `#F08A80` (6.9) |

Ratios are against the editor background; every colour also stays at or above 4.5:1 on the current line and on the selection (`SyntaxThemeTests` enforces it).

## Keyboard focus
- One ring everywhere: 2px Accent, shown only for keyboard focus (`:focus-visible`), never on mouse clicks. Controls with their own border (buttons, inputs, tiles, rows) get an outset ring with a 2px gap; items that fill their container (sidebar rows, tabs, list rows, expander header) get an inset ring.
- Covered: buttons, sidebar navigation, list rows, catalog tiles, onboarding choice rows/tiles, tabs, TextBox, ComboBox, CheckBox, RadioButton, ToggleSwitch, Expander header, the menu button.
- Tab order is visual order: sidebar (Learning group, then settings and collapse), then content top to bottom.

## Landing
The default screen after onboarding and on every start with a profile is Главная (the dashboard); the sidebar marks it active. The catalog stays one click away.

## Do
- Use one primary button per view; use ghost for the rest.
- Keep lists as rows with hairlines; use tiles only for choices.
- Hide empty states of numbers (no zero tiles); show a short plain sentence instead.

## Don't
- No gradients, glass, glow, blobs, emoji as UI, icons beside headings, or fake statistics.
- Do not add a fourth font weight or a second accent color.
- Do not put status color on large surfaces.
