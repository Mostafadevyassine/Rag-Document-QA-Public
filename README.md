#DocQA


DocQA is a Retrieval-Augmented Generation (RAG) app that lets you upload a PDF and ask questions about it in a chat interface. Answers are generated only from text retrieved from the document — never from the model's general knowledge — and every response shows the exact source passages it used, with similarity scores. Built with a React frontend and a .NET backend, using OpenAI for both embeddings and generation, with streaming responses, multi-turn conversation memory, and history-aware query rewriting.