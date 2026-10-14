# AI Lessons

Practice exercises for writing clearer prompts and small programs that use them.

## Day 01 — AI Prompt Lab

A console app that collects five required fields and prints a structured prompt.

| Field | What to enter |
| --- | --- |
| Role | Who the model should act as |
| Task | What it should do |
| Context | The situation or material it should use |
| Output Format | How the answer should be shaped |
| Constraints | Limits it must follow |

Example constraints: “Do not modify source files. Explain the problem before suggesting a fix. Do not invent missing information.”

Blank or whitespace-only values are rejected, and the app asks for that field again. If input ends before a field is provided, the app exits with an error.

### Run

From `Day01/AIPromptLab`:

```bash
dotnet run
```

Requires the .NET 10 SDK.
