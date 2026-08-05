# 008 — Bootstrapping the first account

**Date:** 2026-08-05
**Status:** accepted

## Context

The platform is invitation-only by design. Registration requires an invite; an invite is issued by
an account. On a fresh database there is no account, so nothing can issue an invite, so nobody can
register. The system as specified could not be started.

This was recorded as an open question from task 010 and became blocking at 012, the first task whose
output a person operates.

## Decision

**A one-off command on the server binary creates an account**, bypassing the invite requirement:

```
dotnet Vtt.Server.dll create-account <username>
```

The password is prompted for twice and read without echoing. It is **never** a command-line
argument, never read from configuration, and never written anywhere but the hash column.

The account is created **`Active`** — the one account that never went through an invite and never
needed approving, because there was nobody to approve it.

The branch lives in `Program.cs` after `builder.Build()` and matches the verb exactly.

## Consequences

The credential never touches disk. A password passed as an argument is visible in the process list
to every user on the machine and persists in shell history; read from configuration, it lives in a
compose file or a `.env` that gets copied, committed or backed up. Prompting keeps it in memory and
in the operator's head.

Nothing happens automatically at startup, which is the same stance ADR 003 took for migrations: a
consequential operation is a command someone runs and watches, not a side effect of a restart. It
also means an existing deployment cannot silently grow an account because an environment variable
changed.

**This does not create an administrator, and the distinction matters.** Task 010 deliberately left
the platform-role column to 016, so no account can be marked admin yet. What this creates is the
account that will be marked admin when roles exist, and which in the meantime can do anything at all
because nothing yet checks. Until 016 lands, the platform's protection is that it is not deployed.
That is acceptable for a private tool mid-construction and would not be acceptable a day after
deployment.

`Program.cs` now has three executors — the server, `dotnet ef` at design time, and the integration
tests through `WebApplicationFactory`. The branch matches one exact verb and nothing else, and both
of the other two are verified to still work. A branch that fired on the wrong arguments would break
the migration tooling or the test suite in a way that looks unrelated to its cause.

The command is a permanent capability, not a one-time script that gets deleted. It only creates
accounts and requires shell access to the server, which is an acceptable footing — but it should not
grow the ability to change an existing account's password without revisiting this decision, because
that would turn "can reach the shell" into "can become any user".

## Alternatives rejected

**Seed from configuration on startup.** If no account exists, create one from environment variables.
Trivially automatable and the obvious fit for a container deployment. Rejected because the initial
password would then live in a compose file, a `.env` or shell history — precisely the credential
that must not leak — and because it puts an automatic write at every boot, which is the pattern
ADR 003 already rejected for schema.

**First registration wins.** On an empty database, let the first registration skip the invite and
land `Active`. Zero configuration and no extra code path. Rejected because whoever reaches the
server first becomes the owner of the platform. The window is only between deploying and
registering, but if it is ever missed — a slow first login, a firewall opened early, a redeploy
against an empty volume — a stranger owns a platform holding years of campaign data, and there is no
mechanism to take it back.

**Insert the row by hand in `psql`.** No code at all, which is appealing. Rejected because the
password still has to be hashed with the application's hasher and its exact parameters; producing
that hash outside the application means either a second implementation or pasting a hash generated
somewhere else, and both are worse than a command that already has the hasher in front of it.
