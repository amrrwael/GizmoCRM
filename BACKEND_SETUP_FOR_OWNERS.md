# Setting Up GizmoCRM — The Server Side

This guide is for getting the CRM's server running. You don't need to know how to code to follow
it, but you (or whoever's setting this up) will need to be comfortable running a few commands in
a terminal, since that's how .NET applications get started.

## What you need before you begin

- A computer or server with **.NET 8** installed (this is free from Microsoft's website).
- A place for the database to live — **SQL Server**. This can be a free local install, a Docker
  container, or a cloud database. If you're not sure which, ask whoever's hosting this for you.

## Setting it up

1. Open a terminal and go into the `CRM.Api` folder.
2. Open `appsettings.json` in that folder and find the line that says `ConnectionStrings`. Put
   your database's connection details there. If you're not sure what to put, this is usually
   something your hosting provider or IT person can give you in one line.
3. Run these two commands, one after another:

   ```
   dotnet ef migrations add AddIntegrations --project ../CRM.Infrastructure --startup-project .
   dotnet run
   ```

   The first command only needs to be run once, ever — it sets up the database tables. The
   second one starts the server. You'll know it's working when the terminal shows it's listening
   for requests and doesn't show any red error text.

That's it — the server's running. From here on, whenever you want to start it again, you just
run `dotnet run` from that same folder.

## What you do NOT need to do

You will never need to open a code file to set up Telegram, Gmail, or phone calling. All of that
happens from inside the CRM itself, once you log in, under **Settings → Integrations**. That
page walks you through exactly what to paste in and where to get it from. This was built
specifically so that whoever manages the CRM day to day doesn't need a developer on standby to
turn on a new integration.

## The one thing you do need to do yourself for each integration

Setting up Telegram, Gmail, and Calls requires creating an account with each of those companies —
that part can't be skipped, because it's *their* system, not ours, that hands out the access
codes. But it's genuinely just filling out a form:

- **Telegram** is free. You message a bot called "BotFather" on Telegram, and it hands you a
  token in about thirty seconds.
- **Gmail** is free. You'll need a Google account and to create something called an "OAuth
  Client ID" through Google's developer console — a bit more clicking around than Telegram, but
  no cost and no coding.
- **Calls** is the one paid feature, through a company called Twilio. You sign up, buy a phone
  number (a few dollars, and then it's pay-as-you-go per minute), and copy some codes into the
  CRM.

Every one of those steps is explained, with links and exact instructions, right there on the
Integrations page once the CRM is running. You paste the codes in, click save, and you're
connected.

## If something goes wrong

If the server won't start, the terminal will usually print an error message. The most common
one is the database connection string being wrong or the database not being reachable — that's
almost always worth checking first. Beyond that, this is a good moment to loop in whoever
handles your technical setup, since server errors can have a lot of different causes.
