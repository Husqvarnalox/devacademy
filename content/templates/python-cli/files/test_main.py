from main import greet, main


def test_greet():
    assert greet("Ada") == "Hello, Ada!"


def test_main_prints_greeting(capsys):
    assert main(["Linus"]) == 0
    assert capsys.readouterr().out == "Hello, Linus!\n"


def test_main_default_name(capsys):
    main([])
    assert "world" in capsys.readouterr().out
