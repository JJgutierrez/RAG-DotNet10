# System Requirements Specification: .NET 10 RAG Prototype

This document specifies the concrete requirements, schemas, schemas contracts, and behaviors for the RAG Prototype using the Easy Approach to Requirements Syntax (EARS).

---

## 1. Functional Requirements

### 1.1 Ingestion Pipeline
* **REQ-001 (Idempotent File Ingestion):** 
  * *Requirement:* When a supported source file is added or modified, the **RAG Prototype** shall re-parse, re-chunk, re-embed, and upsert affected chunks idempotently.
  * *Behavior:* Content hashing (SHA-256) of the file determines if it has changed. If unchanged, the ingestion step is skipped. If changed, previous chunks for that file are deleted, and new chunks are generated and inserted.
* **REQ-002 (Idempotent Record Ingestion):** 
  * *Requirement:* When a structured record changes in a registered source table, the **RAG Prototype** shall regenerate the canonical retrieval text and update its embedding without duplicating prior entries.
  * *Behavior:* Uses a unique combination of `SourceId` (e.g., `pg_users`) and `RecordKey` (e.g., primary key value) as a primary key constraint in the vector database to perform an upsert.
* **REQ-006 (PDF Parsing Diagnostics):** 
  * *Requirement:* Where PDF ingestion is enabled, the **RAG Prototype** shall extract text from PDF documents and record extraction failures with file-level diagnostics.

### 1.2 Retrieval & Querying
* **REQ-003 (Similarity Search):** 
  * *Requirement:* When a user submits a chat request, the **RAG Prototype** shall generate a query embedding and retrieve the top-k most relevant chunks from approved sources.
* **REQ-004 (Citations Integration):** 
  * *Requirement:* While a query is being answered, the **RAG Prototype** shall include source identifiers and human-readable citation metadata in the response payload.
* **REQ-005 (Strict Grounding Refusal):** 
  * *Requirement:* If retrieval evidence does not meet the configured relevance threshold or minimum evidence count, then the **RAG Prototype** shall return an explicit refusal response rather than a synthesized answer.

---

## 2. Nonfunctional & Operational Requirements

* **REQ-007 (Structured Telemetry):** 
  * *Requirement:* The **RAG Prototype** shall expose structured logs for ingestion duration, embedding duration, retrieval latency, answer latency, and refusal outcomes.
* **REQ-008 (Graceful Dependency Failures):** 
  * *Requirement:* When local model or database dependencies are unavailable, the **RAG Prototype** shall return typed dependency-failure responses and write correlated error logs.
* **REQ-009 (Interactive Endpoint Protection):** 
  * *Requirement:* The **RAG Prototype** shall apply API rate limiting to interactive endpoints to preserve stability under burst traffic.
* **REQ-010 (Deterministic Evaluation Fixtures):** 
  * *Requirement:* The **RAG Prototype** shall preserve deterministic test fixtures so evaluation results can be reproduced across environments.
* **REQ-011 (Modern Web UI & Interactive Citation Inspector):** 
  * *Requirement:* The **RAG Prototype** shall provide a modern, highly responsive web UI featuring real-time Q&A interactions, interactive citation drawers, relevance score visualizations, and explicit refusal notices.

---

## 3. Data Contracts & Schemas

### 3.1 Vector Chunk Model
Each chunk carries the raw text, its vector embedding, and metadata.

```json
{
  "id": "guid-uuid-string",
  "text": "Extracted text content of the chunk...",
  "embedding": [0.0125, -0.0432, 0.9812], 
  "metadata": {
    "sourceId": "unstructured_docs_01",
    "sourceType": "File" | "DatabaseRecord",
    "documentOrTableName": "architecture_guide.md",
    "recordKey": null,
    "chunkIndex": 3,
    "utcTimestamp": "2026-08-02T01:40:00Z"
  }
}
```

* **Embedding Dimension Contract:** Configurable. Default to **768 dimensions** (matching `nomic-embed-text` in Ollama).

### 3.2 `/api/chat` API Contract

#### Request Payload
```json
{
  "prompt": "What is the security boundary of the RAG prototype?",
  "topK": 5,
  "relevanceThreshold": 0.70,
  "minEvidenceCount": 2
}
```

#### Successful Response Payload (Answer Found)
```json
{
  "correlationId": "corr-uuid-string",
  "answer": "The security boundary is restricted to local components only. System priors are not used for answering without grounding context.",
  "refusalState": {
    "isRefused": false,
    "reason": null
  },
  "citations": [
    {
      "sourceId": "unstructured_docs_01",
      "sourceType": "File",
      "locationName": "constitution.md",
      "recordKey": null,
      "chunkIndex": 1
    }
  ],
  "retrievalSummary": {
    "totalChunksRetrieved": 5,
    "highestRelevanceScore": 0.89,
    "latencyMs": 42
  }
}
```

#### Refusal Response Payload (Insufficient Evidence / Low Relevance)
```json
{
  "correlationId": "corr-uuid-string",
  "answer": null,
  "refusalState": {
    "isRefused": true,
    "reason": "InsufficientEvidence"
  },
  "citations": [],
  "retrievalSummary": {
    "totalChunksRetrieved": 1,
    "highestRelevanceScore": 0.62,
    "latencyMs": 28
  }
}
```

---

## 4. Refusal State Semantics

* **Relevance Threshold Check:** Chunks retrieved via vector similarity search are filtered using `relevanceThreshold` (cosine similarity score).
* **Evidence Count Check:** If the count of remaining chunks after threshold filtering is less than `minEvidenceCount`, the system triggers an automatic refusal.
* **Reasons Defined:**
  * `NoRelevantContext`: Zero chunks met the cosine similarity threshold.
  * `InsufficientEvidence`: Chunks met the threshold, but the count was lower than `minEvidenceCount`.
  * `DependencyFailure`: Downstream vector DB or local inference endpoint returned an error or timed out.
