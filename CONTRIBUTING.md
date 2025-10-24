# CONTRIBUTING

Thanks for your interest in contributing! Below are the contribution guidelines to make reviews and merges faster.

1. Fork & Branching
   - Fork the repository and create a branch with a descriptive name: `feat/<short-description>`, `fix/<short-desc>`, or `docs/<area>`.

2. Coding style
   - The project is C# targeting .NET 8. Keep the existing style (PascalCase for types and methods, camelCase for json where applicable).
   - Keep unrelated files unchanged and avoid large reformatting commits.

3. Commits
   - Write meaningful commit messages, e.g. `feat: add logging to AimBot` or `fix: handle null in ConfigManager`.
   - Group changes logically into commits.

4. Pull Requests
   - Open a PR against the main repository with a clear description: what changed, why, and how to test.
   - Link related issues when applicable.

5. Tests & verification
   - Add unit tests for logic changes. We prefer xUnit but other test frameworks are acceptable.
   - Ensure the solution builds locally: `dotnet build`. Run tests with `dotnet test`.

6. Security & ethics
   - Do not add exploits or code intended to bypass anti-cheat systems or protections.
   - Tools that can be misused should include a clear statement that they are for research/education/testing only.

7. Contact
   - Describe your changes in the PR and use issues to discuss larger refactors before implementation.

Thank you — contributions help the project improve!

