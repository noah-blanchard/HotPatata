# CLAUDE.md

@AGENTS.md

## Claude Code specifics

- Unity work goes through the installed `unity` plugin and its `unity-cli` skill (`unity command ...`). The skill
  covers creating GameObjects and prefabs, reading the Console, running tests, and building.
- `run_script` / `eval_file` compile a scratch C# file against the project assemblies with no domain reload. They are
  handy for building scenes and prefabs via Editor APIs (e.g. `HotPatata.Editor.CourseBuilder.BuildActs`).
- `eval` snippets cannot use `using` directives: write fully qualified names.
