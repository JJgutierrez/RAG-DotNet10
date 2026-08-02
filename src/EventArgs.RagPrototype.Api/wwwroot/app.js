let currentCitations = [];

function switchTab(tabName) {
    document.querySelectorAll('.nav-btn').forEach(btn => btn.classList.remove('active'));
    document.querySelectorAll('.tab-view').forEach(view => view.classList.remove('active'));

    document.getElementById(`nav-${tabName}`).classList.add('active');
    document.getElementById(`view-${tabName}`).classList.add('active');

    const titles = {
        copilot: { title: "Grounded Copilot Interface", sub: "Answer generation strictly restricted to retrieved enterprise evidence with zero hallucination." },
        admin: { title: "Knowledge Ingestion Admin", sub: "Idempotent parsing, embedding generation, and vector persistence." },
        telemetry: { title: "Metrics & System Telemetry", sub: "Real-time query metrics, refusal precision tracking, and latency performance." }
    };

    document.getElementById('tab-title').innerText = titles[tabName].title;
    document.getElementById('tab-subtitle').innerText = titles[tabName].sub;
}

function updateControlValues() {
    document.getElementById('topk-val').innerText = document.getElementById('topk-slider').value;
    document.getElementById('threshold-val').innerText = parseFloat(document.getElementById('threshold-slider').value).toFixed(2);
    document.getElementById('minevidence-val').innerText = document.getElementById('minevidence-slider').value;
}

function sendSuggestedPrompt(text) {
    document.getElementById('chat-input').value = text;
    document.getElementById('chat-form').dispatchEvent(new Event('submit', { cancelable: true }));
}

async function handleChatSubmit(event) {
    event.preventDefault();
    const inputEl = document.getElementById('chat-input');
    const prompt = inputEl.value.trim();
    if (!prompt) return;

    appendMessage('user', prompt);
    inputEl.value = '';

    const topK = parseInt(document.getElementById('topk-slider').value);
    const relevanceThreshold = parseFloat(document.getElementById('threshold-slider').value);
    const minEvidenceCount = parseInt(document.getElementById('minevidence-slider').value);

    const loadingId = appendLoadingMessage();

    try {
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ prompt, topK, relevanceThreshold, minEvidenceCount })
        });

        const data = await response.json();
        removeMessage(loadingId);

        // Update Header & Telemetry Metrics
        if (data.retrievalSummary) {
            const latencyText = `${data.retrievalSummary.latencyMs} ms`;
            document.getElementById('header-latency').innerText = latencyText;
            document.getElementById('telemetry-latency').innerText = latencyText;
        }

        if (data.refusalState && data.refusalState.isRefused) {
            appendRefusalMessage(data.refusalState.reason, data.retrievalSummary);
        } else {
            appendAssistantMessage(data.answer, data.citations);
        }
    } catch (err) {
        removeMessage(loadingId);
        appendErrorMessage(err.message || 'Failed to communicate with local RAG API service.');
    }
}

function appendMessage(role, text) {
    const messagesContainer = document.getElementById('chat-messages');
    const msgDiv = document.createElement('div');
    msgDiv.className = `message ${role}`;
    msgDiv.innerHTML = `
        <div class="avatar">${role === 'user' ? '👤' : '⚡'}</div>
        <div class="message-content"><p>${escapeHtml(text)}</p></div>
    `;
    messagesContainer.appendChild(msgDiv);
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

function appendLoadingMessage() {
    const messagesContainer = document.getElementById('chat-messages');
    const msgDiv = document.createElement('div');
    const id = `loading-${Date.now()}`;
    msgDiv.id = id;
    msgDiv.className = 'message assistant';
    msgDiv.innerHTML = `
        <div class="avatar">⚡</div>
        <div class="message-content"><p><em>Generating grounded response & searching vectors...</em></p></div>
    `;
    messagesContainer.appendChild(msgDiv);
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
    return id;
}

function removeMessage(id) {
    const el = document.getElementById(id);
    if (el) el.remove();
}

function appendAssistantMessage(answer, citations) {
    const messagesContainer = document.getElementById('chat-messages');
    const msgDiv = document.createElement('div');
    msgDiv.className = 'message assistant';

    let citationPillsHtml = '';
    if (citations && citations.length > 0) {
        currentCitations = citations;
        citationPillsHtml = `
            <div class="citations-wrapper">
                <strong>Source Evidence (${citations.length}):</strong>
                ${citations.map((c, idx) => `
                    <button class="citation-pill" onclick="openCitationDrawer(${idx})">
                        📄 [${idx + 1}] ${escapeHtml(c.locationName)} (${(c.relevanceScore * 100).toFixed(1)}%)
                    </button>
                `).join('')}
            </div>
        `;
    }

    msgDiv.innerHTML = `
        <div class="avatar">⚡</div>
        <div class="message-content">
            <p>${formatAnswerText(answer)}</p>
            ${citationPillsHtml}
        </div>
    `;
    messagesContainer.appendChild(msgDiv);
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

function appendRefusalMessage(reason, summary) {
    const messagesContainer = document.getElementById('chat-messages');
    const msgDiv = document.createElement('div');
    msgDiv.className = 'message assistant';

    const highestScore = summary ? (summary.highestRelevanceScore * 100).toFixed(1) : '0';
    const totalRetrieved = summary ? summary.totalChunksRetrieved : 0;

    msgDiv.innerHTML = `
        <div class="avatar">🛡️</div>
        <div class="message-content">
            <div class="refusal-banner">
                <strong>[STRICT GROUNDING REFUSAL - ${escapeHtml(reason)}]</strong>
            </div>
            <p style="margin-top: 0.5rem; font-size: 0.9rem;">
                The copilot refused to answer this query because the retrieved evidence fell below configured safety thresholds.
            </p>
            <ul style="font-size: 0.8rem; color: #fca5a5; margin-left: 1.25rem; margin-top: 0.25rem;">
                <li>Retrieved Candidates: ${totalRetrieved} chunks</li>
                <li>Highest Relevance Score: ${highestScore}%</li>
                <li>Refusal Reason: <code>${escapeHtml(reason)}</code></li>
            </ul>
        </div>
    `;
    messagesContainer.appendChild(msgDiv);
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

function appendErrorMessage(errorMsg) {
    const messagesContainer = document.getElementById('chat-messages');
    const msgDiv = document.createElement('div');
    msgDiv.className = 'message assistant';
    msgDiv.innerHTML = `
        <div class="avatar">⚠️</div>
        <div class="message-content">
            <p style="color: #f87171;"><strong>System Exception:</strong> ${escapeHtml(errorMsg)}</p>
        </div>
    `;
    messagesContainer.appendChild(msgDiv);
}

function openCitationDrawer(index) {
    if (!currentCitations || !currentCitations[index]) return;
    const citation = currentCitations[index];

    const drawerBody = document.getElementById('drawer-body');
    drawerBody.innerHTML = `
        <div class="citation-detail-card">
            <h4>${escapeHtml(citation.locationName)}</h4>
            <div class="citation-detail-meta">
                <div>Source ID: ${escapeHtml(citation.sourceId)}</div>
                <div>Chunk Index: ${citation.chunkIndex}</div>
                <div>Source Type: ${citation.sourceType === 0 ? 'Unstructured File' : 'Structured Table'}</div>
                <div>Relevance Similarity Score: ${(citation.relevanceScore * 100).toFixed(2)}%</div>
            </div>
        </div>
    `;

    document.getElementById('drawer-overlay').style.display = 'block';
    document.getElementById('citation-drawer').classList.add('open');
}

function closeCitationDrawer() {
    document.getElementById('drawer-overlay').style.display = 'none';
    document.getElementById('citation-drawer').classList.remove('open');
}

async function handleIngestSubmit(event) {
    event.preventDefault();
    const filePath = document.getElementById('ingest-path').value.trim();
    if (!filePath) return;

    await executeIngest(filePath);
}

async function quickIngest(fileName) {
    const fullPath = `/Users/sazerac/Eventargs/Projects/dotnet10RAG/sample-data/unstructured/${fileName}`;
    await executeIngest(fullPath);
}

async function executeIngest(filePath) {
    const consoleEl = document.getElementById('ingest-log-console');
    logConsole('info', `[Ingest Triggered] Path: ${filePath}`);

    try {
        const response = await fetch('/api/admin/ingest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ filePath })
        });

        const data = await response.json();
        if (response.ok) {
            if (data.skippedUnchanged) {
                logConsole('warn', `[Idempotent Skip] Content hash unchanged. 0 chunks created.`);
            } else {
                logConsole('success', `[Ingest Succeeded] Ingested ${data.chunksIngested} chunks. SHA256: ${data.contentHash.substring(0, 16)}...`);
            }
        } else {
            logConsole('warn', `[Ingest Error] ${data.error || 'Invalid file path'}`);
        }
    } catch (err) {
        logConsole('warn', `[Network Error] ${err.message}`);
    }
}

function logConsole(type, msg) {
    const consoleEl = document.getElementById('ingest-log-console');
    const div = document.createElement('div');
    div.className = `log-line ${type}`;
    div.innerText = `[${new Date().toLocaleTimeString()}] ${msg}`;
    consoleEl.appendChild(div);
    consoleEl.scrollTop = consoleEl.scrollHeight;
}

function formatAnswerText(text) {
    if (!text) return '';
    return escapeHtml(text).replace(/\n/g, '<br>');
}

function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}
