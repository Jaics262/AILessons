# AI Lessons

A .NET 10 console app, `Day01/AIPromptLab`, built one lesson at a time. Later days stay in that same project. They add Gemini calls, conversation limits, retrieval, and answer checks. The checks from Day 14 onward are deterministic and do not call Gemini.

## Run

From `Day01/AIPromptLab`:

```bash
dotnet run
```

Set `GEMINI_API_KEY` in the environment or in user secrets before a live prompt. The retrieval and answer checks do not need a key.

From the repository root:

```bash
dotnet test Day01/AIPromptLab.Tests/AIPromptLab.Tests.csproj
```

Requires the .NET 10 SDK.

## Day by day

### Day 01 — Structured prompt

The app collects five required fields and prints a structured prompt.

| Field | What to enter |
| --- | --- |
| Role | Who the model should act as |
| Task | What it should do |
| Context | The situation or material it should use |
| Output Format | How the answer should be shaped |
| Constraints | Limits it must follow |

Example constraints: "Do not modify source files. Explain the problem before suggesting a fix. Do not invent missing information."

Blank or whitespace-only values are rejected, and the app asks for that field again. If input ends before a field is provided, the app exits with an error.

### Day 02 — Gemini client

`GeminiLlmClient` reads `GEMINI_API_KEY`, posts the prompt with `HttpClient`, and reads the JSON response. HTTP failures become a clear error. xUnit covers the client. Warnings and errors are written to a logs folder with Serilog.

### Day 03 — Learning summary

A model answer is deserialized into a learning summary with a title, a summary, key points, and a difficulty. A valid summary has exactly three key points. Blank points and any other difficulty are rejected.

### Day 04 — Function calls

The client accepts a Gemini function call, sends the tool result back, and checks the response shape before treating the turn as complete.

### Day 05 — Local calculations

Gemini can call `calculate_total` and `calculate_vat`. One prompt may use at most four tool rounds. After the tools finish, the client asks for the learning-summary schema.

### Day 06 — Conversation history

Each new request includes earlier user messages, model replies, and tool results. A failed request leaves the stored transcript unchanged.

### Day 07 — Recent turns

Only the most recent complete turns are sent. The turn limit and the function-round bound come from `appsettings.json`. A function call stays with its function response. The stored history is not trimmed.

### Day 08 — Retries and hosting

Transient HTTP failures are retried with exponential backoff. A `Retry-After` delay is honored when it is longer than that backoff. Settings load through the generic host.

### Day 09 — Failure logs

Gemini failures are logged by category and HTTP status, without writing the response body. The user is told when the service is temporarily unavailable. A timeout that survives the retries is reported as a timeout.

### Day 10 — History token budget

History is limited to recent complete turns that fit `MaxHistoryTokens`. Selection starts at the newest turn and adds older turns only while both the token budget and the turn limit allow it. The estimate is about four characters per token.

### Day 11 — Summary of older turns

Turns outside the recent window are compressed locally into decisions, constraints, completed actions, and unresolved tasks. That summary is sent with the recent verbatim turns and has its own token budget. It is not written back into the transcript.

### Day 12 — Keyword memories

Stored memories are ranked by keyword overlap and added to the request under a separate token budget. The memory section is a prefix. It is not appended to the transcript.

### Day 13 — Vector memories

`EmbeddingMemoryRetriever` ranks the same memories by cosine similarity of hashed token vectors and sends the top matches. Keyword ranking stays the default. The vectors are deterministic. They are not a Gemini embedding call.

### Day 14 — Retrieval metrics

Ten fixed queries score keyword retrieval and vector retrieval with Precision@3 and Recall@3. Each query expects one memory.

| Metric | Keyword | Vector |
| --- | --- | --- |
| Precision@3 | 0.30 | 0.17 |
| Recall@3 | 0.90 | 0.50 |

### Day 15 — Retrieval quality gate

The report fails when Precision@3 is below 0.25 or Recall@3 is below 0.80. Keyword retrieval passes. The hashed-token vector retriever fails, and the report names both misses.

### Day 16 — Grounded answers

Answers use a controlled comma-separated claim format. A claim passes only when a predefined mapping names it and the context contains that mapping's evidence sentence. The same words in the context are not evidence. "all requests are retried five times" stays unsupported.

### Day 17 — Answer evaluation

Six fixed answers share the Day 16 question and context. The pipeline scores them with those mappings and the two expected facts: the app uses .NET 10, and HTTP retries respect the Retry-After header.

| Scenario | Result |
| --- | --- |
| Correct and grounded answer | Pass |
| Answer contains an unsupported claim | Fail |
| Answer misses an important expected fact | Fail |
| Relevant answer with the wrong fact | Fail |
| Empty answer | Fail |
| Fully correct answer with different wording | Pass |

The different wording passes only because "retries respect Retry-After" is a mapped alias. An unmapped paraphrase fails. The default pass rate is 1, so the fixed set returns an overall fail at 2 of 6. An empty case list returns a failed report that says the dataset is empty. The same input produces the same report.
