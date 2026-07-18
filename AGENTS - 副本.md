# PermissionSystem Project Rules

## Project Overview

This project is an enterprise-grade permission management platform.

Tech Stack:

### Frontend
- Vue 3
- TypeScript
- Vite
- Pinia
- Vue Router
- Axios
- Element Plus

### Backend
- ASP.NET Core Web API (.NET 10)
- EF Core
- SQL Server
- OpenIddict
- Redis
- Serilog

### Architecture
- Frontend/Backend Separation
- Modular Monolith
- DDD-inspired layered architecture

---

# Backend Architecture

## Solution Structure

backend/
├── PermissionSystem.Api
├── PermissionSystem.Application
├── PermissionSystem.Domain
├── PermissionSystem.Infrastructure
├── PermissionSystem.Shared
├── PermissionSystem.Worker

## Layer Responsibilities

### Api Layer
Responsible for:
- Controllers
- Authentication
- Authorization
- Middleware
- Swagger
- Dependency Injection

Rules:
- No business logic
- No direct DbContext access

---

### Application Layer

Responsible for:
- Application Services
- DTOs
- Requests
- Responses
- Use Cases

Rules:
- Contains business workflows
- Coordinates repositories and domain logic
- No infrastructure implementation details

---

### Domain Layer

Responsible for:
- Entities
- ValueObjects
- Domain Services
- Domain Events

Rules:
- Pure business rules
- No EF Core dependencies
- No HTTP dependencies

---

### Infrastructure Layer

Responsible for:
- EF Core
- OpenIddict
- Redis
- Repository
- UnitOfWork
- External Services

Rules:
- No business workflows
- Infrastructure only

---

### Shared Layer

Responsible for:
- ApiResult
- Constants
- Exceptions
- Helpers
- Shared Models

---

# Database Rules

## Database
- SQL Server

## ORM
- EF Core Code First

## Entity Rules

All entities must inherit:

```csharp
BaseEntity
```

BaseEntity contains:
- Id
- TenantId
- CreatedAt
- CreatedBy
- UpdatedAt
- UpdatedBy
- IsDeleted

---

# Authentication & Authorization

## OAuth2 / OpenID Connect

Use:
- OpenIddict official implementation

Supported:
- Password Flow
- Refresh Token
- Client Credentials
- Authorization Code + PKCE

Do NOT implement custom JWT token services.

---

# Permission Model

RBAC Model:

- User
- Role
- Menu
- Permission

Permission strategy:
- PermissionAttribute
- PermissionAuthorizationHandler

Permissions stored in Claims.

---

# API Rules

## RESTful API

Use:
- async/await
- CancellationToken

Return types:
- ApiResult
- PagedResult

Do NOT expose Entity directly.

Use:
- DTO
- Request
- Response

---

# Frontend Rules

## Frontend Structure

src/
├── api
├── assets
├── components
├── directives
├── layouts
├── router
├── stores
├── utils
├── views

---

## Frontend Requirements

Use:
- Composition API
- script setup
- TypeScript

State:
- Pinia

HTTP:
- Axios wrapper
- Auto refresh token

Permissions:
- Dynamic menu
- v-permission directive
- Route guards

---

# UI Rules

Default pages should include:
- Search form
- Table
- Pagination
- Modal form

Use:
- Element Plus

---

# Coding Standards

## Naming

Use:
- Clear naming
- Explicit types
- Avoid magic strings

## Principles

Follow:
- SOLID
- DRY
- High Cohesion
- Low Coupling

---

# Generation Strategy

Always generate code incrementally.

Preferred order:
1. Solution structure
2. Infrastructure
3. Authentication
4. Authorization
5. User/Role/Menu/Permission
6. Frontend framework
7. Business modules

Do NOT generate ERP/WMS modules unless explicitly requested.

---

# Deployment

Use:
- Docker Compose

Default containers:
- SQL Server
- Redis
- Backend API
- Frontend Nginx

---

# Logging

Use:
- Serilog

Minimum logs:
- Request logs
- Error logs
- Audit logs

---

# Documentation

Always generate:
- README.md
- Environment configuration examples
- Setup instructions