Roadmap.md: Production-Ready .NET 10 RAG Prototype for EventArgs LLC
Methodology Note: Under the Specification-Driven Development (SDD) framework, this roadmap is the implementation contract for a local-first, production-style Retrieval-Augmented Generation (RAG) prototype. The specification remains the source of truth, implementation follows verified requirements, and each phase must satisfy explicit validation gates before the next phase begins. 

Positioning Note: This roadmap is designed to support EventArgs LLC's internal knowledge copilot offer by proving secure, source-grounded answers with traceable citations in a controlled local environment before optional Microsoft 365 and Azure integration is added. 

1. Project intent
Build a production-style RAG prototype in .NET 10 that demonstrates citation-grounded question answering over mixed enterprise knowledge sources, strict refusal when evidence is insufficient, deterministic ingestion, and observable retrieval behavior. The prototype should be suitable for client demonstrations and also act as a reference implementation for future Microsoft 365 and Azure-heavy pilot deployments. 

1.1 Product objective

The prototype must answer questions only from approved internal sources, return explicit citations, and refuse unsupported answers when retrieval confidence is too low. This aligns with modern enterprise RAG guidance, where retrieval, grounding, and safe response behavior are core design goals. 

1.2 Deployment objective

The initial implementation runs fully locally by Docker Compose using PostgreSQL with pgvector and Ollama so the team can validate ingestion, embeddings, retrieval quality, and containment without relying on external hosted model services. Ollama is well suited to local development and testing scenarios, and pgvector adds vector similarity search directly inside PostgreSQL. 

1.3 Prototype boundaries

In scope: local document ingestion, structured record ingestion, embedding generation, vector search, grounded answer generation, source citations, observability, rate limiting, and evaluation.

Out of scope for the first prototype: Entra ID, SharePoint sync, Azure-hosted inference, multi-tenant tenancy controls, and distributed ingestion orchestration.

Future-ready requirement: the architecture must preserve a clean seam for later replacement of local inputs with Microsoft 365 and Azure-backed connectors. 

2. System constitution
2.1 Mandatory technology constraints

Runtime: .NET 10 and C# 14, subject to package compatibility verification during Phase 0.

API style: ASP.NET Core Minimal APIs.

AI abstraction layer: Microsoft.Extensions.AI for provider-agnostic chat and embedding integration. 

Vector abstraction layer: Microsoft.Extensions.VectorData.Abstractions for vector store contracts. 

Local model runtime: Ollama for chat and embedding generation; confirm the final package choice during Phase 0 because the Microsoft.Extensions.AI.Ollama package listing indicates deprecation in favor of OllamaSharp. 

Vector store: PostgreSQL with pgvector using cosine similarity and an approximate nearest-neighbor index where benchmark results justify it. pgvector supports vector storage and nearest-neighbor search directly in PostgreSQL. 

2.2 Knowledge source model

The prototype must support two knowledge source classes:

Unstructured corpus: local Markdown, text, and PDF files parsed into normalized text chunks.

Structured corpus: local relational rows from PostgreSQL or SQLite exported into canonical retrieval documents.

Each stored chunk must include source identifiers, source type, filename or table name, record key, chunk index, and timestamp metadata so every answer can point back to evidence. 

2.3 Security and grounding rules

No answer may be generated from model priors alone when the evidence threshold is not met.

The system must return a refusal payload when retrieved evidence falls below configured relevance and coverage thresholds.

The system must record the retrieved evidence identifiers and retrieval scores for every answered request.

The prototype must operate without outbound dependency on hosted LLM endpoints during the main demo path.

These rules make the containment and grounding claims testable instead of aspirational. 

3. Acceptance criteria in EARS form
The EARS method requires ordered clauses and an explicit system name in each requirement. 

3.1 Core functional requirements

REQ-001: When a supported source file is added or modified, the RAG Prototype shall re-parse, re-chunk, re-embed, and upsert affected chunks idempotently. 

REQ-002: When a structured record changes in a registered source table, the RAG Prototype shall regenerate the canonical retrieval text and update its embedding without duplicating prior entries. 

REQ-003: When a user submits a chat request, the RAG Prototype shall generate a query embedding and retrieve the top-k most relevant chunks from approved sources. 

REQ-004: While a query is being answered, the RAG Prototype shall include source identifiers and human-readable citation metadata in the response payload. 

REQ-005: If retrieval evidence does not meet the configured relevance threshold or minimum evidence count, then the RAG Prototype shall return an explicit refusal response rather than a synthesized answer. 

REQ-006: Where PDF ingestion is enabled, the RAG Prototype shall extract text from PDF documents and record extraction failures with file-level diagnostics. 

3.2 Nonfunctional requirements

REQ-007: The RAG Prototype shall expose structured logs for ingestion duration, embedding duration, retrieval latency, answer latency, and refusal outcomes. 

REQ-008: When local model or database dependencies are unavailable, the RAG Prototype shall return typed dependency-failure responses and write correlated error logs. 

REQ-009: The RAG Prototype shall apply API rate limiting to interactive endpoints to preserve stability under burst traffic. ASP.NET Core provides built-in rate limiting middleware for this purpose. 

REQ-010: The RAG Prototype shall preserve deterministic test fixtures so evaluation results can be reproduced across environments. 

4. Phase 0: Specification and architecture baseline
Objective: Lock the specification, confirm package viability, define the data contract, and verify the stack before implementation begins.

Tasks

Confirm package compatibility for .NET 10, including Microsoft.Extensions.AI, Microsoft.Extensions.VectorData.Abstractions, PostgreSQL access packages, and the final Ollama integration package. Microsoft.Extensions.AI.Ollama appears deprecated on NuGet, so this decision must be explicit rather than assumed. 

Write constitution.md, requirements.md, and architecture.md as separate source-of-truth documents.

Define the response schema for /api/chat, including answer text, citations, refusal state, retrieval summary, and correlation ID.

Define the chunk schema, embedding dimension contract, and metadata model.

Define threshold semantics: top-k, minimum accepted score, minimum evidence count, and refusal behavior.

Validation gate

Package proof-of-compatibility documented.

Specification artifacts reviewed and approved.

One end-to-end sequence diagram completed for ingest and query flow.

5. Phase 1: Local infrastructure and solution scaffold
Objective: Create a reproducible local environment and a clean solution structure that supports growth into a production-style reference implementation.

Tasks

Create docker-compose.yml with PostgreSQL plus pgvector and Ollama services.

Add service health checks and deterministic startup ordering.

Create solution projects:

EventArgs.RagPrototype.Domain

EventArgs.RagPrototype.Application

EventArgs.RagPrototype.Infrastructure

EventArgs.RagPrototype.Api

EventArgs.RagPrototype.Tests

Add baseline configuration management, typed options, and environment-specific settings.

Add developer scripts for up, down, ingest, test, and seed-demo.

Validation gate

docker compose up -d completes successfully.

Health checks for PostgreSQL and Ollama report healthy.

dotnet build succeeds across the full solution.

The API starts and returns a basic readiness endpoint.

6. Phase 2: Ingestion pipeline
Objective: Normalize mixed data sources into deterministic retrieval documents with traceable metadata.

Tasks

Implement document readers for Markdown, plain text, and PDF.

Implement structured record readers for relational sources with configurable table mappings.

Normalize extracted content into a canonical IngestionDocument model.

Implement chunking policy with configurable token or character windows and overlap.

Store source lineage metadata on every chunk.

Add file fingerprinting or content hashing for idempotent re-ingestion.

Create an ingestion command or worker that processes sources in batches.

Design notes

Use chunk sizes and overlaps that are configurable rather than hardcoded so retrieval quality can be tuned through evaluation.

Structured rows should be rendered into descriptive, retrieval-friendly text templates rather than raw serialized JSON unless benchmarking proves otherwise.

Validation gate

Integration tests verify no duplicate chunks on repeated ingestion.

Golden-file tests verify consistent chunk boundaries for the same input.

Parse failures are captured with diagnostics instead of silent drops.

dotnet test passes all ingestion tests.

7. Phase 3: Embeddings and vector persistence
Objective: Generate embeddings locally and persist searchable vectors with metadata integrity.

Tasks

Integrate the final selected Ollama client for embedding generation behind an application abstraction.

Create vector persistence tables and migrations in PostgreSQL with pgvector enabled.

Store embeddings, chunk text, and all citation metadata in a searchable schema.

Benchmark exact search vs approximate index configuration and choose HNSW or IVFFlat only after measuring retrieval behavior on the demo corpus. pgvector supports approximate nearest-neighbor patterns, but the final index choice should be evidence-based. 

Implement batch upsert logic with retry and idempotency semantics.

Validation gate

Embedding dimensions are validated against the configured model.

Stored vectors can be queried successfully using cosine similarity.

Metadata integrity tests confirm filename, source ID, chunk index, and timestamps survive round trips.

Nearest-neighbor smoke tests return expected neighbors for seeded fixtures.

8. Phase 4: Retrieval and answer generation
Objective: Implement the RAG execution loop with strict grounding and refusal behavior.

Tasks

Generate query embeddings for incoming questions.

Retrieve top-k candidate chunks from the vector store.

Apply threshold filtering and minimum evidence rules before answer generation.

Construct a prompt that instructs the model to answer strictly from retrieved context and refuse unsupported claims.

Return answer text plus structured citations and retrieval diagnostics.

Implement a refusal contract containing a machine-readable reason such as NoRelevantContext, InsufficientEvidence, or DependencyFailure.

Prompt requirements

The system prompt must forbid unsupported extrapolation.

The context block must preserve citation identifiers.

The answer format must separate narrative answer text from citation objects.

Validation gate

Contract tests confirm grounded responses include citations.

Failure-path tests confirm low-confidence queries return refusals.

Cross-source tests confirm the system can retrieve evidence from both unstructured and structured inputs.

9. Phase 5: API surface and operational controls
Objective: Expose a demo-safe API with validation, rate controls, and clear operational boundaries.

Tasks

Implement /api/chat for interactive grounded Q&A.

Implement /api/admin/ingest or a CLI-only ingestion path instead of a public /api/ingest endpoint.

Add request validation and typed error responses.

Add ASP.NET Core rate limiting on interactive endpoints. 

Add correlation IDs and request logging.

Add readiness and liveness endpoints.

Validation gate

Microsoft.AspNetCore.Mvc.Testing contract tests pass for success, refusal, validation failure, and dependency failure cases.

Admin-only ingestion path is protected by environment or role configuration.

Rate limiting behavior is verified with automated tests. 

10. Phase 6: Resilience, logging, and observability
Objective: Make runtime behavior measurable and support production-style diagnosis during demos and future pilot delivery.

Tasks

Add structured logging with correlation IDs.

Capture metrics for ingestion throughput, retrieval latency, answer latency, refusal rate, and dependency failures.

Add resilience policies for local dependency communication, including retries only where safe and bounded.

Record retrieval scores and selected evidence IDs for every answered request.

Produce a lightweight query trace artifact for debugging groundedness issues.

Validation gate

Logs show end-to-end correlation across ingest and chat flows.

Simulated PostgreSQL and Ollama outages produce typed failures rather than hangs.

Observability output is sufficient to explain why a query answered or refused.

11. Phase 7: Evaluation and demo readiness
Objective: Prove the prototype is demonstrable, reproducible, and aligned with EventArgs' citation-first positioning.

Tasks

Create an evaluation set of at least 10 to 20 benchmark questions covering both source types.

Score retrieval hit rate, citation completeness, refusal precision, and answer groundedness.

Add benchmark cases for unsupported questions to prove refusal behavior.

Create a scripted demo path showing ingestion, grounded answer generation, source citations, and observability output.

Package a clean demo dataset and reset script.

Validation gate

Evaluation suite passes the agreed thresholds.

All benchmark questions produce either supported cited answers or correct refusals.

A fresh environment can be started, seeded, and demonstrated from scripts alone.

12. Deliverables
The completed prototype must produce the following artifacts:

Source code for the .NET 10 solution.

docker-compose.yml and local environment instructions.

Specification documents: constitution.md, requirements.md, architecture.md, and evaluation-plan.md.

Demo dataset and reset scripts.

Automated tests covering ingestion, retrieval, API contracts, and evaluation flows.

A short demo guide describing the expected walkthrough.

13. Future expansion path
After the local-first prototype succeeds, the next implementation track can replace local corpus adapters with enterprise connectors for SharePoint, Azure file sources, and permission-aware business systems while preserving the same retrieval, citation, and refusal contracts. This maintains continuity with EventArgs LLC's public positioning around secure internal knowledge copilots for Microsoft 365 and Azure-heavy teams. 