# Development environment setup — from nothing to writing code

Windows machine, empty folder, no tooling installed. Follow this top to bottom once. Budget an
hour, most of it waiting for downloads.

---

## 0. The shape of what you are building

Before typing anything, understand the layout, because it explains every decision below.

Windows hosts two things: **Docker Desktop** and **VS Code**. Everything else — the code, the .NET
SDK, Node, git, Claude Code — lives inside **WSL2**, a real Linux kernel running alongside Windows.
Your project folder is a Linux path (`~/projects/vtt`), not a Windows path.

Why: your production server runs Linux. Developing on Linux means the environment you test in
matches the one you deploy to, and an entire class of "works on my machine" problems never happens.
WSL2 gives you that without dual-booting.

**The one rule that causes the most pain if broken:** never put the project under `/mnt/c/...`.
WSL2 can reach your Windows drives through `/mnt/c`, but every file operation then crosses a
translation layer. Builds crawl, and file-watching (hot reload, test watchers) silently fails to
notice changes. Keep everything on the Linux side.

---

## 1. Install WSL2

Open **PowerShell as Administrator** (Start menu → type "PowerShell" → right-click → Run as
administrator) and run:

```powershell
wsl --install
```

This enables the required Windows features and installs Ubuntu as the default distribution.
**Restart your computer** when prompted.

After restart, Ubuntu launches automatically and asks for a username and password. This is your
Linux account, unrelated to your Windows login. Pick something short. The password is what `sudo`
will ask for — you will type it often, and it does not echo characters as you type. That is normal.

Verify from PowerShell:

```powershell
wsl --list --verbose
```

You want to see `Ubuntu` with `VERSION 2`. If it says `1`, run `wsl --set-version Ubuntu 2`.

**Also install Windows Terminal** if you do not have it (Microsoft Store, or
`winget install Microsoft.WindowsTerminal`). It gives you tabs and a proper Ubuntu profile,
and is far more pleasant than the default console.

---

## 2. Prepare Ubuntu

Open Ubuntu (Windows Terminal → dropdown → Ubuntu). Everything from here runs inside Linux.

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y curl wget git build-essential unzip ca-certificates
```

`build-essential` provides the compilers and headers that various tools expect to exist.

Configure git — the name and email get baked into every commit you make:

```bash
git config --global user.name "Your Name"
git config --global user.email "your@email.com"
git config --global init.defaultBranch main
```

---

## 3. Install the .NET SDK

Try the distribution package first:

```bash
sudo apt install -y dotnet-sdk-10.0
```

If that reports the package is not found, your Ubuntu release does not carry that version yet.
Use Microsoft's install script instead, which does not need root:

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel LTS
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools' >> ~/.bashrc
source ~/.bashrc
rm dotnet-install.sh
```

Verify:

```bash
dotnet --info
```

You should see an SDK version listed. If `dotnet` is not found, your shell has not picked up the
new PATH — close the terminal and reopen it.

Check the current LTS version at https://dotnet.microsoft.com/download if you want to confirm
what you installed is current.

---

## 4. Install Node.js

Use **nvm** (Node Version Manager) rather than apt. Ubuntu's packaged Node is usually old, and nvm
lets you switch versions per project without fighting the system.

Get the current install command from https://github.com/nvm-sh/nvm (it embeds a version number
that changes), run it, then reopen your terminal and:

```bash
nvm install --lts
nvm use --lts
node --version
npm --version
```

---

## 5. Install Docker Desktop

This one goes on **Windows**, not inside Ubuntu. Download from
https://www.docker.com/products/docker-desktop and install with the WSL2 backend option enabled.

Then open Docker Desktop → **Settings → Resources → WSL Integration** → enable integration for
Ubuntu → Apply & Restart.

Back in your Ubuntu terminal, verify Docker is reachable from Linux:

```bash
docker --version
docker run hello-world
```

If `docker` is not found, the WSL integration toggle did not take. Re-check that setting and
restart Docker Desktop.

**What Docker is doing here:** rather than installing PostgreSQL directly onto your machine, you
run it as a container — an isolated, disposable process with its own filesystem, described in a
config file that lives in the repo. This means your database setup is reproducible, your machine
stays clean, and what you run locally is the same image you run on the VPS.

---

## 6. Install Claude Code

Inside Ubuntu:

```bash
curl -fsSL https://claude.ai/install.sh | bash
```

This is the native installer — it downloads a self-contained binary, sets up PATH, and keeps
itself updated in the background. Node is not required for it.

Reopen your terminal, then verify:

```bash
claude --version
claude doctor
```

`claude doctor` prints installation and settings diagnostics without starting a session — use it
whenever something behaves oddly.

If you get `command not found`, the binary landed at `~/.local/bin/claude` but that directory is
not on your PATH. Fix:

```bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
source ~/.bashrc
```

**Account requirement:** Claude Code needs a Pro, Max, Team, Enterprise, or Console account. The
free Claude.ai plan does not include access. On first run, `claude` opens a browser to log in; if
the callback hangs, copy the printed URL into any browser manually and paste the code back.

---

## 7. Install VS Code

Install VS Code on **Windows** from https://code.visualstudio.com, then add the **WSL** extension
(Microsoft). Also install the **C# Dev Kit** extension.

The point of the WSL extension: the editor UI runs on Windows, but the language servers, terminal,
and debugger all run inside Linux, against your Linux files. You get correct paths for free.

From your Ubuntu terminal, inside a project folder, typing `code .` opens that folder in VS Code
properly connected to WSL. The bottom-left corner should read `WSL: Ubuntu`.

---

## 8. Set up GitHub access

Generate an SSH key inside Ubuntu:

```bash
ssh-keygen -t ed25519 -C "your@email.com"
```

Accept the default path, set a passphrase if you want one. Then start the agent and add the key:

```bash
eval "$(ssh-agent -s)"
ssh-add ~/.ssh/id_ed25519
cat ~/.ssh/id_ed25519.pub
```

Copy the printed public key. On GitHub: **Settings → SSH and GPG keys → New SSH key**, paste, save.

Test it:

```bash
ssh -T git@github.com
```

A greeting message means it works. ("Shell access is not provided" in the response is expected —
GitHub does not give you a shell, only git access.)

Now create an empty repository on GitHub named `vtt` (private, no README — you already have files).

---

## 9. Put the project in place

```bash
mkdir -p ~/projects
cd ~/projects
```

Copy the scaffold zip into WSL. If it is in your Windows Downloads folder:

```bash
cp /mnt/c/Users/<YourWindowsName>/Downloads/vtt-scaffold.zip .
unzip vtt-scaffold.zip
mv vtt-scaffold vtt
rm vtt-scaffold.zip
cd vtt
```

This is the one legitimate use of `/mnt/c` — copying a file across once. The project itself now
lives at `~/projects/vtt`, on the Linux side.

Initialise the repository and make the first commit:

```bash
git init
git add .
git commit -m "Project documentation, plan, and Claude Code configuration"
git remote add origin git@github.com:<your-username>/vtt.git
git push -u origin main
```

### Start the database

From task 002 onward the project expects PostgreSQL to be running in a container. Docker Desktop
must be open, with WSL integration enabled (§5).

```bash
cp .env.example .env
```

Open `.env` and set `POSTGRES_PASSWORD` to anything — it never leaves your machine, and the file is
gitignored. Then:

```bash
docker compose up -d
docker compose ps          # postgres should read "healthy" within a few seconds
```

To poke at the database, use the client inside the container — nothing to install on your side:

```bash
docker compose exec postgres psql -U vtt -d vtt
```

Start the server through the script rather than `dotnet run`: .NET does not read `.env`, and the
script is what turns it into the connection string the server expects.

```bash
./scripts/ef.sh database update       # create/update the schema — see below
./scripts/dev-server.sh
curl http://localhost:5080/api/health
# -> {"status":"Healthy","checks":{"database":"Healthy"}}
```

A 200 from `/api/health` means the process is up **and** it can reach the database. Stop the container
and the server keeps running, but `/api/health` turns into a 503 naming the check that failed. That is
the intended behaviour: an unreachable database is a condition to report, not a reason to crash.

If the container never reaches "healthy", `docker compose logs postgres` says why. The usual causes
are port 55432 already being in use, or a stale data volume from an earlier attempt —
`docker compose down -v` clears the second one, at the price of the local data.

### Start the frontend

In a **second terminal**, leaving the server running in the first:

```bash
cd src/Client
npm install          # first time only
npm run dev
```

Open <http://localhost:5173>. The page fetches the health endpoint and reports what came back, so it
doubles as a check that both halves of the stack are talking to each other. Three outcomes, all of
them useful:

| What you see | What it means |
|---|---|
| `Healthy`, database `Healthy` | Everything works |
| `Unhealthy`, database `Unhealthy` | Frontend, proxy and server are fine; Postgres is not running |
| Server unreachable | `scripts/dev-server.sh` is not running |

There is deliberately **no script that starts both halves**. Two watchers interleaving their output
into one terminal is harder to read than two windows, and the first thing you do when a combined
script misbehaves is run the halves separately anyway.

The frontend never calls port 5080 directly. `vite.config.ts` proxies `/api` to the server, so the
browser only ever sees one origin — which is what will let the session cookies in task 013 work
without CORS or a local HTTPS certificate. Always write relative paths (`/api/...`); anything
hardcoding `localhost:5080` works here and breaks in production.

### Running the tests

```bash
dotnet test                                      # backend
cd src/Client && npm run test                    # frontend
```

The backend suite has two halves. Most tests are ordinary unit tests and need nothing running. The
ones in `tests/Server.Tests/Integration/` start **their own PostgreSQL container**, apply the
migrations to it, boot the real application against it, and throw the container away at the end.

That means two things worth internalising:

- **`dotnet test` needs Docker running**, and will fail confusingly if it is not. When Docker is
  unavailable, `dotnet test --filter "Category!=Integration"` runs the rest in about a fifth of a
  second.
- **The tests cannot damage your development database.** They never connect to the compose
  container. You can run the suite in the middle of a session without thinking about it.

Frontend tests use Vitest with jsdom — a simulated DOM, good enough to render components and query
them the way a user would perceive them, and not good enough for anything involving real layout or
a canvas. `npm run test:watch` reruns on save.

Why a real database rather than a fast fake is [ADR 005](decisions/005-integration-tests-against-a-real-database.md).

### Working with migrations

The database schema is not written by hand. You change the C# model, and EF Core generates a
**migration**: a class with an `Up` method describing the change and a `Down` method undoing it.
Alongside them EF keeps a *model snapshot* — its picture of what the schema currently looks like —
and the database keeps a `__EFMigrationsHistory` table listing which migrations it has run. Those
three things together are what let a migration be generated once and applied anywhere.

Always go through `scripts/ef.sh` rather than `dotnet ef`. The tools build the server and run
`Program.cs` as far as `builder.Build()`, so the connection-string check runs at design time too;
the script supplies the same environment `dev-server.sh` does.

```bash
./scripts/ef.sh migrations add AddSomething --output-dir Infrastructure/Migrations
./scripts/ef.sh database update
./scripts/ef.sh migrations list
```

**Read the generated file before committing it.** EF infers the migration by diffing your model
against the last snapshot, and the diff is not always the change you had in mind — renaming a
property looks exactly like dropping one column and adding another, which is a silent data loss if
you apply it without looking.

**Fixing a migration you got wrong.** Before it is merged this is cheap:

```bash
./scripts/ef.sh database update 0    # revert to an empty database (or name an earlier migration)
./scripts/ef.sh migrations remove    # delete it and roll the snapshot back
```

Run `migrations remove` on a migration that is still applied and it refuses:

> The migration '...' has already been applied to the database. Revert it and try again.

That is the tool enforcing the order, not an error to work around. Note that `remove` needs the
database reachable, because it checks what is applied — running it with the container down leaves
the snapshot and the database disagreeing about reality.

**After a migration is merged to `main`, it is frozen.** Correct it with a *new* migration, never by
editing the old one. Anyone who has already applied it has a row in `__EFMigrationsHistory` and will
never run that file again, so an edit changes the schema for new databases only, and the two silently
diverge. This is the "migrations are append-only once merged" rule in `CLAUDE.md`.

Migrations are never applied automatically when the server starts. That is a deliberate choice with
consequences for deployment — [ADR 003](decisions/003-migrations-applied-explicitly.md) explains why.

---

## 10. First Claude Code session

```bash
cd ~/projects/vtt
claude
```

**Before asking it to do anything, verify it can see its instructions.** Type:

```
/context
```

Under **Memory files** you should see `CLAUDE.md` and the files from `.claude/rules/`. If they are
missing, Claude Code cannot follow them, and nothing else in this setup matters. The usual cause is
launching from the wrong directory — instructions are found by walking up from where you started.

Then a sanity check before real work:

```
Read docs/plan/roadmap.md and docs/plan/tasks/001-repo-scaffold.md, then tell me in your own
words what task 001 requires and what is deliberately out of scope. Do not write any code.
```

If the answer is accurate, the configuration is working. If it invents scope or ignores the
exclusions, stop and fix that before building anything on top of it.

**Then start task 001:**

```
Let's do task 001. Enter plan mode and propose a file-level plan. Do not write code until I approve.
```

---

## 11. The daily loop

One task, one session, one branch. Concretely:

1. `cd ~/projects/vtt && claude`
2. Name the task: *"Let's do task 0NN. Plan first."*
3. **Plan mode** — cycle permission modes with **Shift+Tab** until it shows plan mode, or simply
   instruct it to plan without writing code. Read the plan properly. This is the cheapest moment
   in the entire process to catch a wrong direction — a bad plan costs a minute to fix here and
   an afternoon to fix after implementation.
4. Approve. It creates `task/0NN-name` and implements.
5. Review the diff yourself:
   ```bash
   git diff main...HEAD          # everything on the branch
   git diff main...HEAD --stat   # just which files changed, to orient first
   ```
6. Read its explanation. If any part is unclear, ask — that is the arrangement, and an unclear
   explanation means the task is not finished.
7. Approve, let it update `docs/plan/PROGRESS.md`, then merge and push:
   ```bash
   git checkout main
   git merge task/0NN-name
   git push
   ```
8. **Exit and start a fresh session for the next task.** Long sessions accumulate context that
   dilutes adherence to the rules files.

### Commands worth knowing

| Command | What it does |
|---|---|
| `/context` | Shows what actually loaded, including memory files. First stop when behaviour is wrong |
| `/memory` | Browse and edit CLAUDE.md and the notes Claude writes itself |
| `/init` | Regenerates a starting CLAUDE.md — do **not** run it here, yours is hand-written |
| `/compact` | Compresses conversation history when a session gets long |
| `claude doctor` | Installation and settings diagnostics, from the shell |

### When it goes wrong

- **It ignored an instruction.** Run `/context` and confirm the file loaded. Remember that
  CLAUDE.md is context, not enforcement — Claude reads and tries to follow it, but compliance is
  not guaranteed. For rules that must hold absolutely, use a PreToolUse hook, which runs as a
  shell command regardless of what the model decides.
- **It corrected the same thing twice.** That belongs in CLAUDE.md or a rules file. Ask it to add
  the rule, or edit the file yourself.
- **It went beyond the task card.** Stop the session, discard the branch (`git checkout main &&
  git branch -D task/0NN-name`), and restart with a tighter card. Do not review sprawling diffs;
  you will miss things.

---

## 12. Ready-to-develop checklist

Each of these should print something sensible from inside `~/projects/vtt`:

```bash
pwd                 # /home/<you>/projects/vtt   — NOT /mnt/c/...
dotnet --info       # an SDK version
node --version      # an LTS version
docker ps           # a table, not an error
docker compose ps   # postgres, "healthy" (once you have run `docker compose up -d`)
git remote -v       # your GitHub repo over SSH
claude --version    # a version number
```

Plus, inside a `claude` session, `/context` lists `CLAUDE.md` and your rules files.

All green means you are ready to start task 001.

---

## Never develop against the VPS

The production server will hold the only live copy of campaign data. Develop locally, deploy
through CI. An agent running experimental migrations against production is the fastest way to lose
years of someone's campaign.
