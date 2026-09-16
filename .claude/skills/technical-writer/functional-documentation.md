# Skill: Functional Documentation Structure

Load this skill when writing the functional (stakeholder-facing) document.

## Audience assumption

The reader does not code and does not care about implementation details.
They care about what changed for the business/users and how to use it.

## Required sections

- **What this does**: plain-language description, no jargon.
- **Why it matters**: the business value or problem solved.
- **How it works (user perspective)**: a Mermaid `flowchart` of the user's
  journey, described in terms of what the user sees/does, not internal
  system steps.
- **Frequently Asked Questions**: anticipate the 3-5 questions a
  stakeholder or support person would actually ask.

## Style rules

- No code snippets, no class/method names, no HTTP status codes.
- Replace technical terms with their business meaning (e.g. "the system
  automatically retries three times" instead of "exponential backoff with 3
  attempts").
- Keep it short. If a technical section doesn't translate into something a
  non-technical reader cares about, leave it out.
