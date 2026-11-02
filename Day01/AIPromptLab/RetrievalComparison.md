# Retrieval strategy comparison

Both strategies searched the same 10 queries in `RetrievalEvaluationSet` and the same 10 memory texts. Each number is the mean of the 10 per-query scores. K is 3.

Keyword retrieval is `KeywordMemoryScorer`. Vector retrieval is `EmbeddingMemoryRetriever` with `HashedTokenEmbedder`. Those vectors are deterministic token hashes, not a Gemini embedding call.

Each query expects one memory, and both retrievers return three memories. A query whose expected memory is in that top three scores 1/3 for precision and 1 for recall.

| Metric | Keyword | Vector |
| --- | --- | --- |
| Precision@3 | 0.30 | 0.17 |
| Recall@3 | 0.90 | 0.50 |

Keyword precision is exactly 0.30, from 9 of 10 expected memories appearing in the top three. Vector precision is exactly 1/6, about 0.17, from 5 of 10. The memory list itself is left unchanged.
