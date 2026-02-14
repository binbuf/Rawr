# Task 1: Project Structure & Refactoring

## Goal
Refactor the existing single-project solution into a Clean Architecture/Onion Architecture style structure to separate concerns as defined in the Design Document.

## Steps
1.  **Rename/Move Existing Project**:
    *   Move the current `Rawr` project logic (which is likely the UI) into a new folder/project named `src/Rawr.UI`.
    *   Update the `.csproj` and `.slnx`/`.sln` files accordingly.

2.  **Create Core Library**:
    *   Create a new Class Library project: `src/Rawr.Core` (Target: .NET 10.0).
    *   This project will hold Interfaces, Domain Models, and pure business logic.
    *   **No dependencies** on UI frameworks or heavy infrastructure libraries (where possible).

3.  **Create Infrastructure Library**:
    *   Create a new Class Library project: `src/Rawr.Infrastructure` (Target: .NET 10.0).
    *   This project will implement interfaces from `Core` (e.g., FileSystem access, Audio implementations, System API calls).
    *   Add dependency on `Rawr.Core`.

4.  **Create Test Projects**:
    *   `tests/Rawr.Core.Tests` (xUnit).
    *   `tests/Rawr.Infrastructure.Tests` (xUnit).

5.  **Dependency Setup**:
    *   `Rawr.UI` should reference `Rawr.Infrastructure` (for DI injection) and `Rawr.Core`.
    *   Ensure all projects build successfully.

## Verification
*   `dotnet build` passes with no errors.
*   The solution structure matches:
    ```
    /
    ├── src/
    │   ├── Rawr.Core/
    │   ├── Rawr.UI/
    │   └── Rawr.Infrastructure/
    └── tests/
        ├── Rawr.Core.Tests/
        └── Rawr.Infrastructure.Tests/
    ```
