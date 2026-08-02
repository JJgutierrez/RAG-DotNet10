#!/usr/bin/env bash
set -e

echo "Starting local infrastructure (PostgreSQL with pgvector & Ollama)..."
docker compose up -d

echo "Waiting for PostgreSQL..."
until docker compose exec -T postgres pg_isready -U raguser -d ragdb > /dev/null 2>&1; do
    echo -n "."
    sleep 1
done
echo "PostgreSQL is ready!"

echo "Checking Ollama..."
until docker compose exec -T ollama ollama list > /dev/null 2>&1; do
    echo -n "."
    sleep 1
done
echo "Ollama is ready!"

echo "Pulling required embedding model (nomic-embed-text)..."
docker compose exec -T ollama ollama pull nomic-embed-text || true

echo "Pulling default chat model (llama3.2)..."
docker compose exec -T ollama ollama pull llama3.2 || true

echo "Infrastructure is up and running!"
