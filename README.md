# TaxiRank

A passenger booking system for long-distance trips. Passengers can search for
a trip, book a seat, pay securely online via **Stripe**, and receive a ticket
once payment is confirmed.

## Features

- 🚕 **Trip booking** — passengers browse available long-distance trips and
  reserve a seat.
- 💳 **Stripe payment integration** — secure online checkout for each booking.
- 🎫 **Ticket generation** — a ticket is issued automatically once payment
  goes through.
-  **SQL Server backend** — trip, passenger, and booking data is persisted
  in a relational database (see `SQLQuery1.sql` for the schema).

## Tech stack

| Layer       | Technology                     |
|-------------|---------------------------------|
| Backend     | C# / ASP.NET                    |
| Frontend    | HTML, CSS, Razor/ASPX views     |
| Database    | SQL Server (T-SQL)              |
| Payments    | Stripe                          |

## Project structure

```
TaxiRank/
  TaxiRank/           Main application project
  SQLQuery1.sql        Database schema / setup script
  TaxiRank.sln         Visual Studio solution file
```

## Getting started

### Prerequisites

- Visual Studio 2022 (with the **ASP.NET and web development** workload)
- SQL Server (Express edition works fine) or LocalDB
- A [Stripe](https://stripe.com) account for API keys (test mode is fine for development)

### Setup

1. **Clone the repo**
   ```
   git clone https://github.com/ahiwe/TaxiRank.git
   cd TaxiRank
   ```

2. **Create the database**
   Run `SQLQuery1.sql` against your SQL Server instance to create the
   required tables.

3. **Configure the connection string**
   Update the connection string in the project's configuration file
   (`Web.config` / `appsettings.json`) to point to your local database.

4. **Add your Stripe API keys**
   Add your Stripe publishable and secret keys to the app's configuration
   (never commit real keys — use a `.gitignore`d local settings file or
   environment variables).

5. **Open and run**
   Open `TaxiRank.sln` in Visual Studio and press **F5** to build and run.

## How it works

1. A passenger searches for a long-distance trip and selects one.
2. They enter passenger details and proceed to checkout.
3. Payment is processed through Stripe.
4. On successful payment, a ticket is generated and made available to the
   passenger as confirmation of their booking.

## Roadmap ideas

- Email delivery of the ticket (PDF) after booking
- Admin view for managing trips and viewing bookings
- Cancellation / refund flow

