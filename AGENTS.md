# AGENTS.md

## Project Overview

This project is a SAP Business One integration platform.

Architecture:

React UI
    ↓
ASP.NET Core API
    ↓
SAP Business One Service Layer
    ↓
SAP Business One

No SAP UI API.
No SAP DI API.

All SAP interactions must occur through SAP Business One Service Layer APIs.

---

## Technology Stack

Frontend:
- React
- TypeScript
- Vite
- Material UI

Backend:
- ASP.NET Core 9
- Entity Framework Core
- PostgreSQL

External Systems:
- SAP Business One Service Layer

---

## Architectural Principles

Frontend Responsibilities:

- UI rendering
- Form validation
- User interactions
- API consumption

Backend Responsibilities:

- Business logic
- SAP integration
- Authentication
- Authorization
- Audit logging

Never call SAP Service Layer directly from React.

All SAP communication must pass through ASP.NET Core APIs.

---

## Folder Structure

Backend:

src/

    Application/
    Domain/
    Infrastructure/
    Api/

Frontend:

src/

    pages/
    components/
    hooks/
    services/
    models/

---

## SAP Service Layer Rules

All SAP operations must use:

- OData endpoints
- Service Layer APIs
- Session-based authentication
- Paginations, filters, sorts
- always consider that the data will be huge, and based on that limit api calls, and consider that no memory leakage is present or caused in the .net api application


Never:

- Access SAP database directly
- Use DI API
- Use UI API
- Execute SQL against SAP tables

Forbidden:
- UPDATE OPOR
- UPDATE OCRD
- UPDATE OINV
- Direct SQL connections to SAP databases

---

## SAP Authentication

Create dedicated SAP session management.

Requirements:

- Login
- Session reuse
- Session renewal
- Logout

Avoid logging into SAP on every request.

Implement centralized session handling.
---

## SAP Services

Create service abstraction.

Example:

ISapService
ISapPurchaseOrderService
ISapBusinessPartnerService
ISapItemService

Business logic must not contain HTTP calls.
Only Infrastructure layer may communicate with SAP.

---

## API Standards

All APIs must return:

{
  "success": true,
  "message": "",
  "data": {}
}

Error format:

{
  "success": false,
  "message": "",
  "errors": []
}

---

## Purchase Order Rules

Source of truth:

SAP Business One

Do not store SAP transactional data locally unless required for:

- Caching
- Audit history
- Reporting

Purchase Orders must be retrieved from SAP Service Layer.

---

## Business Partner Rules

Operations:

- Get BP
- Search BP
- Create BP
- Update BP

Use Service Layer entities only.

Do not duplicate master data locally.

---

## Error Handling

SAP errors must be transformed into user-friendly API responses.

Never expose raw SAP exceptions to frontend.

Log:

- Request
- Response
- Status code
- Correlation ID

---

## Logging

Log:

- User ID
- SAP endpoint
- Request duration
- Correlation ID

Do not log:

- SAP passwords
- Session tokens
- Sensitive customer information

---

## Frontend Standards

Use:

- Functional components
- TypeScript strict mode
- React hooks

Avoid:

- Business logic inside components
- Direct fetch calls

Use service classes:

services/
    purchaseOrderService.ts
    businessPartnerService.ts

---

## State Management

Prefer:

- React Query
- TanStack Query

Avoid excessive local state.

Server data should be cached using query providers.

---

## Security

Use:

- JWT Rotation authentication
- Role-based authorization

Never expose:

- SAP credentials
- Service Layer URLs
- Internal system details

All SAP credentials must remain server-side.

---

## Performance

Requirements:

- Pagination for large SAP datasets
- Server-side filtering
- Server-side sorting

Avoid loading entire SAP tables.

---

## Testing

Backend:

- Unit tests
- Integration tests

Frontend:

- Component tests
- API integration tests

Mock SAP Service Layer during tests.

Never connect to production SAP.

---

## Definition of Done

Feature is complete only when:

- React UI implemented
- API implemented
- SAP Service Layer integration completed
- Validation completed
- Logging completed
- Error handling completed
- Tests added
- Documentation updated

---

## Agent Instructions

When implementing features:

g. Create DTOs first.
2. Create API endpoints.
3. Create SAP service abstractions.
4. Implement Service Layer integration.
5. Implement frontend services.
6. Implement UI.
7. Add validation.
8. Add tests.

Always follow Clean Architecture.

Never bypass the API layer.
Never access SAP databases directly.
Never use SAP DI API.
Never use SAP UI API.

# References
Refer SapForms Project for existing implemented features.

## Core Principles
1. Never modify SAP system tables directly.
2. Never execute direct UPDATE statements against SAP business objects.
3. No changes in SAPforms blazor project
4. Use UI reusable components always, if required create them in UI
5. Consider the security aspects from all sides and ensure the approach is secure and does not pose a vulnerability
6. Follow the best practices for security and data protection.
7. Maintain proper audit logs in the db which will be able to showcase to the users.
