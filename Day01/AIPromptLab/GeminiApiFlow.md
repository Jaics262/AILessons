# Gemini generateContent flow

A real prompt calls `v1beta/models/{model}:generateContent`. The default model is `gemini-3.8-flash`, and the default timeout is 5 minutes. The API key is sent in the `x-goog-api-key` header.

History and the stored summary change only after the model returns a valid learning summary. Every stop before that leaves them as they were.

Green means the turn is saved. Blue means another attempt or another `generateContent` call. Red means the call stops and the stored history stays unchanged.

## Before the request

```mermaid
flowchart TD
  key{API key present?}
  key -->|No| missing[MissingApiKey<br/>No request sent]
  key -->|Yes| prompt{Prompt blank?}
  prompt -->|Yes| blank[ArgumentException<br/>No HTTP call]
  prompt -->|No| rank{Embedding retriever supplied?}
  rank -->|No| keyword[Keyword rank<br/>Top 3]
  rank -->|Yes| embed[Cosine rank<br/>Top 3]
  keyword --> assemble[Assemble contents]
  embed --> assemble
  assemble --> order[Summary, memories,<br/>recent turns, prompt]
```

Keyword ranking is the default. Cosine ranking runs only when an `EmbeddingMemoryRetriever` is supplied. Memories that were never indexed are skipped. A blank query returns no memories.

The assembled request can contain:

1. A compact summary of older turns, within the summary token budget.
2. Up to three retrieved memories, within the memory token budget.
3. Recent complete turns, within the turn and history token limits.
4. The current prompt.

The summary and the memory section are a prefix. They are sent to the model and are not written into the transcript.

## HTTP attempt

Each call sends either the `calculate_total` and `calculate_vat` tool declarations, or the learning-summary JSON schema. It does not send both.

There are at most three attempts: the first call plus two retries.

```mermaid
flowchart TD
  post[POST generateContent]
  post --> outcome{Attempt result}
  outcome -->|User cancelled| cancel[Stop immediately]
  outcome -->|429 or 500 to 599<br/>and attempts remain| wait[Wait, then retry]
  outcome -->|Network failure or timeout<br/>and attempts remain| wait
  outcome -->|Any other status| classify[Classify this response]
  outcome -->|Last attempt| classify
  wait --> backoff[200 ms, then 400 ms,<br/>or Retry-After if longer]
  backoff --> post
```

Cancellation during the wait also stops immediately. It is not turned into `RequestTimeout`.

## Status and body

This classification uses the response from the last attempt.

```mermaid
flowchart TD
  status{Final result}
  status -->|2xx JSON| read[Read model parts]
  status -->|429| rate[RateLimitExceeded]
  status -->|401, 403, or invalid-key body| badKey[InvalidApiKey]
  status -->|Other non-2xx| rejected[ServerRejection]
  status -->|Network error on the last attempt| network[NetworkFailure]
  status -->|Timeout on the last attempt| timeout[RequestTimeout]
  status -->|Null or unreadable JSON| empty[EmptyResponse]
```

`RESOURCE_EXHAUSTED` in the body is also `RateLimitExceeded`. `API_KEY_INVALID` or `API key not valid` in the body is `InvalidApiKey`.

## Model response

The client allows at most four tool rounds. While a tool round remains, and the model has not yet been asked to format a summary, tools are enabled. Otherwise the next call uses the summary schema.

A valid learning summary has a title, a summary, exactly three key points, and a difficulty of beginner, intermediate, or advanced.

```mermaid
flowchart TD
  parts{Model parts}
  parts -->|Empty text| emptyText[EmptyResponse]
  parts -->|Valid summary| save[Save the turn]
  parts -->|Prose while tools are open| format[Ask for summary JSON<br/>Schema on, tools off]
  parts -->|Prose when schema is required| invalid[InvalidResponse]
  parts -->|calculate_total| total[Quantity integer<br/>Unit price number]
  parts -->|calculate_vat| vat[Amount and rate]
  parts -->|Unknown name or bad arguments| badCall[InvalidFunctionCall]
  parts -->|Tool call after the round limit| badCall
  total --> again[POST generateContent again]
  vat --> again
  format --> again
  again --> parts
```

Several tool calls in one response are executed before the follow-up request. The API key is removed from model text before it is stored or sent again.

## Saved turn

```mermaid
flowchart TD
  save[Valid summary] --> summary[Replace stored summary<br/>Older-turn facts, or none]
  summary --> append[Append this turn<br/>Prompt, tool exchange, model text]
  append --> prefix[Leave the prefix unstored<br/>Summary section and memories]
  prefix --> result[Return summary, function calls, and totals]
```

A failed call does not call `Replace` or `ReplaceSummary`.

The worker prints a `GeminiClientException` and waits for the next prompt. Typing `reset` as the role clears the session locally and does not call the API.
