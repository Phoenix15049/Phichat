# PhiChat Backend

A secure messaging backend built with **ASP.NET Core 8**.\
PhiChat provides the server-side infrastructure for a real-time
encrypted messaging system, including authentication, message delivery,
chat key management, and user communication features.

> Backend repository of PhiChat.

## Features

-   Real-time messaging using **SignalR**
-   Encrypted message storage (server stores encrypted content)
-   Chat key management for private conversations
-   JWT-based authentication
-   User registration and login
-   Phone-based authentication support
-   Message delivery and read status tracking
-   Reply and forward message support
-   File attachment support
-   Message reactions
-   Contact management
-   User online/offline status
-   Global exception handling middleware
-   Entity Framework Core database layer
-   Docker support

## Architecture

The project follows a layered architecture:

    Phichat.API
        └── Controllers, SignalR Hub, Middleware

    Phichat.Application
        └── DTOs, Interfaces, Validation Rules

    Phichat.Domain
        └── Entities and Core Models

    Phichat.Infrastructure
        └── Database, Services, External Implementations

## Security Approach

PhiChat is designed around encrypted communication:

-   Messages are received and stored as encrypted data.
-   The backend does not process plaintext message content.
-   Chat keys are managed separately from message data.
-   Authentication is handled using JWT tokens.

> For a complete end-to-end encryption flow, the client application is
> responsible for encryption and decryption operations.

## Technologies

-   **.NET 8**
-   **ASP.NET Core Web API**
-   **SignalR**
-   **Entity Framework Core 8**
-   **SQL Server**
-   **JWT Authentication**
-   **FluentValidation**
-   **Docker**

## Project Structure

    Phichat
    │
    ├── Phichat.API
    │   ├── Controllers
    │   ├── Hubs
    │   └── Middleware
    │
    ├── Phichat.Application
    │   ├── DTOs
    │   ├── Interfaces
    │   └── Validators
    │
    ├── Phichat.Domain
    │   └── Entities
    │
    └── Phichat.Infrastructure
        ├── Data
        ├── Services
        └── Migrations

## Getting Started

### Requirements

-   .NET 8 SDK
-   SQL Server
-   Docker (optional)

### Configuration

Update database and authentication settings in:

    Phichat.API/appsettings.json

Example:

``` json
{
  "ConnectionStrings": {
    "DefaultConnection": "your_database_connection"
  }
}
```

### Database Migration

Run:

``` bash
dotnet ef database update
```

### Run the API

``` bash
dotnet run --project Phichat.API
```

The API will start on the configured application URL.

## Real-Time Communication

PhiChat uses SignalR for:

-   Sending messages instantly
-   Receiving online status updates
-   Tracking user presence
-   Delivering real-time events

Hub:

    /chatHub

## API Documentation

Swagger is available in development mode:

    /swagger

## Screenshots / Demo

![Message Flow](./screenshots/demo.gif)

Recommended additions:

-   Swagger API overview screenshot
-   Database diagram
-   Real-time message flow GIF
-   Client + server communication demo

## Future Improvements

Possible improvements:

-   Stronger password hashing strategy (Argon2 / BCrypt)
-   Refresh token support
-   More advanced encryption key lifecycle management
-   Rate limiting
-   Automated testing
-   Production deployment configuration

## License

This project is licensed under the terms defined in the repository
license.
