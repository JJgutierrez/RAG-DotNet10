#!/usr/bin/env bash
set -e

API_URL="${1:-http://localhost:5000}"

echo "=== EventArgs RAG Prototype Demo Seeder ==="
echo "Target API: $API_URL"

FILES=(
    "$(pwd)/sample-data/unstructured/copilot_architecture.md"
    "$(pwd)/sample-data/unstructured/employee_policy.md"
    "$(pwd)/sample-data/unstructured/server_incident_response.txt"
)

for FILE in "${FILES[@]}"; do
    if [ -f "$FILE" ]; then
        echo "Ingesting file: $(basename "$FILE")..."
        curl -s -X POST "$API_URL/api/admin/ingest" \
             -H "Content-Type: application/json" \
             -d "{\"filePath\": \"$FILE\"}" | jq . 2>/dev/null || true
        echo ""
    else
        echo "Warning: Sample file $FILE not found."
    fi
done

echo "Demo dataset seeding completed!"
