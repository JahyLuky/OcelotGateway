# Ocelot API Gateway

## Overview

This service is the API Gateway for the microservices architecture. It acts as a single, unified entry point for all client requests. Its primary responsibilities are to route incoming requests to the appropriate backend service, handle authentication, and role-based access control.

## Core Functionality

*   **Request Routing:** It uses the `ocelot.json` configuration file to map public-facing upstream routes (e.g., `/api/database/...`) to the internal downstream services (e.g., `DatabaseServiceApi`).
*   **Authentication and Authorization:** It serves as the central authentication authority. It secures the backend services by validating JSON Web Tokens (JWTs) on incoming requests before forwarding them. Unauthorized requests are rejected at the gateway.
*   **Secret Injection:** After authenticating a request, the gateway adds a secret header (`X-Gateway-Secret`) to the downstream request. This allows the backend services to trust that the request has been vetted by the gateway.
*   **Unified API Documentation:** It provides a single Swagger UI that aggregates the OpenAPI specifications from all backend services, offering a comprehensive view of the entire system's API surface.
*   **Load Balancing:** It is configured with a custom `PrimaryBackup` load balancer to manage traffic to downstream services.

## Authentication Flow

1.  A client application sends its credentials (e.g., `ClientId` and `ClientSecret`) to the gateway's `/auth/token` endpoint.
2.  The gateway validates the credentials and, if successful, returns a signed JWT.
3.  The client includes this JWT as a Bearer token in the `Authorization` header for all subsequent API requests.
4.  The gateway validates the JWT on each request before routing it to the appropriate downstream service.

## Configuration

The gateway's behavior is primarily controlled by two files:

### `ocelot.json`

This file defines the core routing rules. Each route mapping specifies:
*   `UpstreamPathTemplate`: The public-facing URL path that clients will request.
*   `DownstreamPathTemplate`: The internal path on the downstream service.
*   `DownstreamHostAndPorts`: The location of the downstream service.
*   `AuthenticationOptions`: Specifies that a valid JWT (`Bearer`) is required to access the route.

### `appsettings.json`

This file contains the gateway's own configuration settings:

*   **`Kestrel`**: Configures the gateway's own HTTP and HTTPS endpoints, including the SSL certificate.
*   **`JwtSettings`**: Contains the secret `Key` for signing and validating JWTs, the `Issuer` and `Audience` details, and a list of valid `Clients` with their credentials and roles.
*   **`GatewaySecret`**: The secret value that the gateway injects into the `X-Gateway-Secret` header for downstream services.
*   **`SwaggerEndpoints`**: A list of URLs pointing to the `swagger.json` files of the backend services, which are used to build the unified Swagger UI.

## Swagger

API documentation is available via Swagger at `<ocelot_api>/api/swagger`
