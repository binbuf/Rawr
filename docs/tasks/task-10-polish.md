# Task 10: Final Polish & Packaging

## Goal
Prepare the application for use.

## Steps
1.  **OS Start**:
    *   Implement logic to add Registry Key (Windows) or `.desktop` file (Linux) for "Start with Windows".

2.  **Credential Storage**:
    *   Implement secure storage for Basic Auth passwords if strictly needed (using `CredentialManagement` package for Windows). *Low priority if PrivateUrl is primary*.

3.  **Release Build**:
    *   Configure `publish` profiles.
    *   Ensure assets (sounds, icons) are copied correctly.
    *   Test `Release` mode (optimizations can sometimes break reflection/serialization).

## Verification
*   Full end-to-end test of a "Fresh Install" scenario.
