# Skill: Technical Documentation Structure

Load this skill when writing the technical (engineer-facing) document.

## Audience assumption

The reader can read code and understands the stack, but was not involved in
building this feature. Write for "a competent engineer joining the team
next month."

## Required sections

- **Overview**: 2-4 sentences, what this is and where it lives in the
  system.
- **Architecture**: components involved and how they connect. Include a
  Mermaid `flowchart` or `graph` diagram.
- **Key Components**: the non-obvious classes/modules/services and their
  responsibilities.
- **Data Flow / Sequence**: for anything with a multi-step process, include
  a Mermaid `sequenceDiagram`.
- **Edge Cases & Error Handling**: what can fail and what happens.
- **Extension points**: if relevant, how a future engineer would extend this
  safely.

## Style

- Prefer diagrams and short paragraphs over long prose blocks.
- Link to actual file paths in the repo instead of restating code inline
  when a short reference suffices.
- Do not document things you have not verified against the actual code —
  read the source before writing "how it works."
