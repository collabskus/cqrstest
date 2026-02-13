Update the CI mode logic in MultiDbSync.Console to better demonstrate CQRS benefits. 

Technical Requirements:
1. Implement concurrent readers: In addition to the writing threads, implement multiple reading threads.
2. Load simulation: For every 'n' products added, ensure the system performs at least '10 * n' total reads.
3. Architecture: Closely follow the existing custom CQRS pattern in MultiDbSync.Application. Use modern .NET 10 standards, including Primary Constructors and the 'readonly' modifier where applicable.
4. Dependencies: Avoid adding new NuGet packages. Specifically, do not use MediatR. If a package is strictly necessary, ensure it is free and the latest version.

Processing Instructions:
- Read 'dump.txt' in its entirety to understand the full context before responding. Do not skim or extract portions.
- Provide the FULL content of all modified or new files for easy copy-pasting. 
- If my architectural assumptions are incorrect based on the source, please provide a comprehensive explanation in your response.
- 
