# Skill: Coding Standards Adherence

Load this skill before writing or modifying any code.

## Before writing a single line

1. Find and read at least 2-3 existing files similar to what you're about to
   write (same layer, same kind of component) to learn naming, structure,
   and idioms actually used in this repo.
2. Check for a linter/formatter config (`.eslintrc`, `.editorconfig`,
   `pyproject.toml`, `.prettierrc`, `checkstyle.xml`, etc.) and follow it.
3. Check for a style guide file in the repo (`CONTRIBUTING.md`, `STYLE.md`,
   `CLAUDE.md`) and follow it — it overrides generic conventions.

## In-code documentation comments

- Use the language configured in `xmlDocComments` (default `es`).
- Document the *why*, not a restatement of the code. Skip comments on
  self-explanatory code.
- Keep the doc comment format native to the language (XMLDoc for C#, JSDoc
  for JS/TS, docstrings for Python, PHPDoc for PHP, Javadoc for Java, etc.)
  even though the *content* is in the configured language.

## General discipline

- Prefer small, focused functions/methods over large ones.
- Reuse existing utilities/helpers instead of duplicating logic.
- Handle errors explicitly; do not swallow exceptions silently.
- Do not leave commented-out code, debug prints, or TODOs without context.
