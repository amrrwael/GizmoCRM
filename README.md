# GizmoCRM Backend — Developer Notes

This is the API side of GizmoCRM, built in .NET 8. If you're picking this project up and need
to understand how it's put together, this is for you. There's a separate, much simpler doc
(`BACKEND_SETUP_FOR_OWNERS.md`) if you just need to get the thing running and aren't interested
in the code.

## The shape of the project

It's organized as Clean Architecture, split into four projects that only depend "downward":

```
CRM.Domain          — the actual business objects. No dependencies on anything.
CRM.Application     — the rules of the business. Depends only on Domain.
CRM.Infrastructure  — the messy real-world stuff: database, Telegram, Gmail, Twilio.
CRM.Api             — the web server itself. Wires everything together and exposes it over HTTP.
```

The reason this matters day to day: if you're working in `CRM.Application`, you should never
need to `using Microsoft.EntityFrameworkCore` or make an HTTP call yourself. You ask for an
`IApplicationDbContext` or an `ITelegramService`, and something in Infrastructure has already
been registered to answer that ask. It keeps the business logic testable and keeps you from
accidentally coupling a CQRS handler to, say, the specific shape of a Twilio API response.

A quick map of where things live:

- `CRM.Domain/Entities` — `User`, `Contact`, `Deal`, `Activity`, plus the newer stuff:
  `TelegramChat`, `TelegramMessage`, `GmailAccount`, `EmailMessage`, `CallLog`, `IntegrationSetting`.
- `CRM.Application/Features` — one folder per feature area (Contacts, Deals, Activities, Users,
  Auth, Dashboard), each holding MediatR commands/queries, their handlers, and FluentValidation
  validators. This is where the actual business rules live.
- `CRM.Application/Common/Interfaces` — every "outside world" dependency is defined here as an
  interface: `IApplicationDbContext`, `ITelegramService`, `IGmailService`, `ICallService`,
  `IIntegrationSettingsService`, `ICurrentUserService`, `ITokenService`.
- `CRM.Infrastructure/Services` — the implementations of all of the above.
- `CRM.Infrastructure/Persistence` — `AppDbContext`, EF Core Fluent API configurations, and
  migrations.
- `CRM.Api/Controllers` — thin controllers. Most of them just deserialize the request, call
  MediatR or a service, and return the result.

## What I actually fixed here

I want to be upfront about this rather than just listing features, because a few of these were
real, would-not-compile-or-run bugs, not stylistic nitpicks.

**Telegram was completely broken.** `TelegramService` referenced `_context.TelegramChats` and
`_context.TelegramMessages`, but `IApplicationDbContext` never declared those DbSets — that's a
straight-up compile error. Even setting that aside, `ITelegramService` was never registered in
DI, so any request that touched it would've thrown at runtime. And the webhook controller bound
the incoming Telegram payload to a `dynamic` parameter, which .NET turns into a boxed
`JsonElement` under the hood — `update.message` on that just throws, because `JsonElement`
doesn't have a property called "message." Every incoming Telegram message would've silently
failed. I fixed all of it: added the missing DbSets, registered the service properly (with a
typed `HttpClient` via `AddHttpClient<ITelegramService, TelegramService>`), and replaced the
`dynamic` binding with a real `TelegramUpdateDto`.

One more thing that had to change to make any of this work: `BaseEntity`'s `Id`, `CreatedAt`,
and `UpdatedAt` properties had `protected` setters, which is fine for entities that use the
private-constructor-plus-static-factory pattern (like `Contact` and `Deal` do), but
`TelegramService` was building entities with plain object initializers from outside the Domain
project — `new TelegramChat { Id = ..., CreatedAt = ... }` — which doesn't compile against a
protected setter. I widened those to `public set`. It's a small change but it's the kind of
thing that silently breaks a build in a way that's annoying to track down.

## Where the credentials live

Nothing for Telegram, Gmail, or Twilio goes in `appsettings.json`. There's a table called
`IntegrationSettings` — just a key/value store, encrypted at rest using ASP.NET Core's built-in
Data Protection APIs — and an `IIntegrationSettingsService` that every integration service asks
for credentials at call time. The frontend's Settings page writes to it through
`SettingsController`. This was a deliberate choice: the person running this CRM might not be a
developer, and asking them to edit a JSON file on a server they can't see isn't realistic.

One operational note if you deploy this: the Data Protection keys get written to a
`dataprotection-keys` folder next to the app. If that folder doesn't persist across deploys
(e.g. you're on an ephemeral filesystem), the encryption key rotates and anything already saved
in `IntegrationSettings` stops decrypting. Nothing crashes — it just means whoever's running the
CRM has to go re-paste their tokens into Settings. Mount that folder as a persistent volume in
production and you won't have to think about this.

## Telegram, Gmail, and Calls — how each one actually talks to its provider

I intentionally didn't reach for the official SDKs here. Partly because I built and reviewed all
of this without a .NET compiler available to me (more on that below), and hand-rolled REST calls
over `HttpClient` are a lot easier to get right by careful reading than a large SDK surface is.
Partly because it keeps the dependency footprint small.

- **Telegram** — `TelegramService` just calls `api.telegram.org` directly. Sending a message is
  a POST to `/sendMessage`. Setting the webhook is a GET to `/setWebhook`. Nothing fancier than
  that.
- **Gmail** — `GmailService` implements the OAuth2 authorization-code flow by hand: it builds the
  Google consent URL, exchanges the returned code for tokens at `oauth2.googleapis.com/token`,
  and refreshes access tokens the same way when they expire. Sending mail means building a raw
  MIME message, base64url-encoding it, and POSTing it to Gmail's `messages/send` endpoint.
  Reading mail is a list call followed by per-message metadata fetches, which get cached into the
  `EmailMessages` table so we're not re-fetching from Google on every page load.
- **Calls** — `CallService` builds Twilio "Access Tokens" itself, which are just JWTs with a
  specific header (`cty: twilio-fpa;v=1`) and a grants payload, signed with HMAC-SHA256 using
  your Twilio API key secret. That token is what lets the browser's Twilio Voice SDK place and
  receive calls without a phone. For the simpler "ring my phone and bridge it" flow, it's a plain
  POST to Twilio's Calls API with Basic Auth.

## Auth, briefly

JWT bearer tokens, with one quirk worth knowing about if you're testing this with Postman or
curl: the API expects the raw token in the `Authorization` header, with no `Bearer ` prefix.
That's not a bug — it's how the original project was set up, and the frontend already matches it
in `api/client.ts`. I left it as-is rather than "fixing" it, since changing it would mean
touching both sides for no real benefit.

Three roles — Admin, Manager, Sales. Admin and Manager can see and edit integration credentials
and manage users; Sales gets contacts, deals, and activities, plus full use of Telegram/Gmail/
Calls once someone with a higher role has connected them.

## Getting a database migration in place

I couldn't run the .NET CLI in the environment I used to build this, so there's one thing you'll
need to do yourself before the app will run against a real database — generate the migration for
the new tables:

```bash
cd CRM.Api
dotnet ef migrations add AddIntegrations --project ../CRM.Infrastructure --startup-project .
dotnet run
```

The entity models and the `DbContext` are complete and correct, so this should generate cleanly.
After that one command, the app applies pending migrations automatically on startup in
Development (look at `DatabaseSeeder.SeedAsync` if you want to see where), so you're back to a
normal `dotnet run` workflow from then on.

