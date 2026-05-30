wilson.txt
----------
You will be creating Wilson, a simple app that allows a user to have a conversation with a language model hosted on a backend Ollama or OpenAI compatible instance.

There will be a backend C# component and a frontend React dashboard.  The frontend React dashboard should be gated by an authentication/login screen, which requires the user to provide a server URL and an access key.  The backend will be a C# server.  You must follow the implementation requirements set forth in C:\code\agents for frontend, backend, testing, UI/UX, and everything else.

The server should have a settings JSON file that indicates the LIST of backend model runners, where each definition provides 1) API type, 2) endpoint details, 3) optional list of models (list of models will not be required if it's an Ollama endpoint).

When the user authenticates, it compares the access key vs what is stored in Wilson's database, which should be multitenant according to the markdown found in c:\code\agents.  The system needs to be multi-tenant, and you only need to focus on Sqlite and Postgres as backend database implementations today.

Once authenticated, the user should be presented with a ChatGPT like experience, and it should be styled as such.  You should see a list of conversations on the left (these need to be stored in the database), along with a chat window where a user can input prompts and see responses from a model selected via 1) a drop-down selector indicating the server and 2) a drop-down selector indicating the model.  The front-end and backend should support non-streaming AND streaming responses, end to end.

Context should be preserved between turns, and automatically truncated when context window limits are being approached.

An admin dashboard should be available allowing the user to manage tenants, users, credentials, API explorer, request history, and the server settings file (which should be editable via the dashboard and via API).

I need you to lean a TON on the implementation and styling found in:
- c:\code\conductor\conductor
- c:\code\assistanthub
- c:\code\pneuma
- c:\code\litegraph\litegraph
- c:\code\lattice
- c:\code\partio
- c:\code\recalldb

So the UX is this:
- Connect to the dashboard webserver, and login either as a user or as an admin user
- As a user
  - See my conversation history, and be able to reload it to get back to the same state
  - Start a new conversation
  - History automatically truncates messages based on context window
  - Give feedback (thumbs up, thumbs down, free-form text feedback)
  - Start a new chat with 1) the selected server and 2) the selected model on that server
  - Styled like OpenAI/ChatGPT
- As an admin
  - Manage tenants, users, credentials
  - Manage conversation history, feedback
  - See the API Explorer and request history including charts, styled and functionally the SAME as the aforementioned projects

You must follow the requirements in c:\code\agents.  There needs to be 1) a docker compose setup including docker/factory/ like the referenced projects, 2) repository setup (.gitignore, README, CHANGELOG), etc.  Use PolyPrompt NuGet package for inference API integration.  Use Watson7 for the webserver.  Support CORS and have CORS settings in the settings JSON file.  

- C# source in c:\code\wilson\src
- Dashboard in c:\code\wilson\dashboard

Ask any questions necessary before beginning.