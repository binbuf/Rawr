# Task 10: Final Polish & Packaging

## Goal
Prepare the application for use.

## Steps
1.  **OS Start**:
    *   Implement logic to add Registry Key (Windows) or `.desktop` file (Linux) for "Start with Windows".

2.  **Credential Storage**:
    *   Implement secure storage for Basic Auth passwords if strictly needed (using `CredentialManagement` package for Windows). *Low priority if PrivateUrl is primary*.

3. **Add data reset**:
  * Add a button in Settings to erase all calendar event data and start fresh.

4. Add configurable option to hide UI popup if fullscreen borderless or fullscreen exclusive (and their equivalaents on MacOS and Linux)