# AIFlow CLI - Human-AI Collaborative Flow Control System

**Version:** 1.7 (Conceptual)

## Overview

AIFlow CLI is a command-line interface tool designed to structure, manage, and facilitate iterative collaboration between human users and AI assistants. It helps organize tasks, associated project resources (like files, documents, or plans), and the exchange of information, making complex projects involving AI more traceable and efficient.

While initially conceived with software development in mind, AIFlow v1.7 has been generalized to support a variety of project types, such as managing family chores, house maintenance schedules, or research for large purchases, through the use of customizable "Flow Templates."

The current version (v1.x series) primarily facilitates interaction by generating structured JSON payloads for the human to copy-paste to an AI, and then helps integrate the AI's structured JSON response back into the project.

## Key Features (v1.7)

* **Task Management:** Create, update, view, list, and archive tasks with detailed metadata (description, status, assignee, branch).
* **Agile & GitFlow Inspired:** Supports Agile-style task metadata (type, priority, story points, sprint, labels, due dates) and GitFlow-inspired branch naming conventions for organizing tasks.
* **Resource Tracking:** Manage project resources (files, URLs, text snippets) associated with tasks, including their status and versioning (via hashes for local files).
* **Structured AI Interaction:**
    * `prepare-input`: Generates structured JSON requests for the AI, detailing the task and relevant resources. Supports multi-part requests for large inputs.
    * `integrate-output`: Parses structured JSON responses from the AI to update tasks and resources.
* **Conflict Handling:** Detects concurrent local modifications and provides options to overwrite, skip, or generate Git-style merge conflict markers directly in the conflicted file for manual resolution.
    * `resolve`: Marks a manually merged file as resolved.
* **Safety & Validation:**
    * `--dry-run` mode for `integrate-output` to preview changes.
    * Backup & Revert (`revert`): Automatically backs up project state before AI integration and allows reverting to a previous state.
    * `.aiflowignore`: File to specify patterns for excluding resources from AI requests.
    * Basic validation of AI JSON payload structure.
    * Token count estimation for AI requests to warn about potential context window issues.
* **Flow Templates:**
    * Initialize new AIFlow projects from user-defined templates (`aiflow_templates.json`) to quickly set up common project structures, initial tasks, resources, and roadmaps (e.g., for "Family Chores," "House Maintenance," "Car Shopping").
* **Usability & Information:**
    * Interactive mode for `prepare-input` to guide users through task creation.
    * Insightful `status` command: Shows unmanaged local changes, overdue tasks, and task status summaries.
    * Advanced task filtering for `task list`.
    * `summary` command for a high-level project dashboard view.
    * Localization support for CLI messages (via `Resources.resx`).

## Getting Started

### Prerequisites

* .NET 8 SDK (or a compatible .NET runtime if using a published version).

### Building from Source (Conceptual)

1.  **Create Project:** `dotnet new console -n AIFlow.Cli -f net8.0`
2.  **Populate Files:** Use the provided code chunks (e.g., via a script like `CreateHearthenStructure.ps1` or by manually creating files) to populate the `AIFlow.Cli` project with all C# source files (`Program.cs`, files in `Models/`, `Services/`, `Commands/`) and the `Properties/Resources.resx` file.
3.  **Add NuGet Packages:**
    ```bash
    dotnet add package System.CommandLine
    dotnet add package Microsoft.Extensions.FileSystemGlobbing
    ```
4.  **Configure `Resources.resx`:**
    * Ensure `Resources.resx` is in a `Properties` folder.
    * Set its "Build Action" to `Embedded resource`.
    * Set its "Custom Tool" to `PublicResXFileCodeGenerator` (or `ResXFileCodeGenerator`).
5.  **Build:** `dotnet build`
6.  The executable will be in `bin/Debug/net8.0/aiflow-cli` (or similar).

### Initializing a Project

1.  Navigate to your project's root directory.
2.  Run: `aiflow-cli init`
    * This creates `aiflow.json` (the main configuration and state file) and a sample `.aiflowignore` file.
    * It also creates a sample `aiflow_templates.json` file.
3.  **Using a Flow Template (Optional):**
    * Edit `aiflow_templates.json` to define your project templates.
    * Then run: `aiflow-cli init --template <YourTemplateName>`
    * Or, if your templates are in a different file: `aiflow-cli init --template <YourTemplateName> --template-file path/to/your/templates.json`

## Core Commands

* **`aiflow-cli init [--template <name>] [--template-file <path>]`**: Initializes a new AIFlow project.
* **`aiflow-cli prepare-input [options...]`**: Prepares a structured JSON request for the AI. Can run interactively if few options are provided.
    * Key options: `--task-id`, `--task-desc`, `--resource <path>`, `--new-resource <path>`, `--notes <text>`, and various Agile metadata flags (`--type`, `--priority`, etc.).
    * Use `--continue-task <task-id>` for multi-part requests.
* **`aiflow-cli integrate-output "<AI_JSON_PAYLOAD>" [--dry-run]`**: Integrates the AI's JSON response, updating tasks and resources.
    * `--dry-run`: Simulates integration without making changes.
* **`aiflow-cli status [--detailed] [--branch <name>] [--include-archived]`**: Shows project status, unmanaged changes, overdue tasks, and task summaries.
* **`aiflow-cli task <subcommand> [options...]`**: Manages tasks.
    * `list`: Lists tasks with advanced filtering.
    * `view <task-id>`: Shows detailed task information.
    * `update <task-id> [options...]`: Updates task metadata.
    * `note <task-id> -m "<message>"`: Adds a human-authored note to a task.
    * `archive <task-id>`: Archives a task.
* **`aiflow-cli branch [<branch-name>]`**: Lists or conceptually creates branches.
* **`aiflow-cli checkout <branch-name>`**: Sets the active branch for new tasks.
* **`aiflow-cli resolve <resource-path>`**: Marks a resource with merge conflicts as resolved.
* **`aiflow-cli revert [--list | --id <backup-id> | --last] [--force]`**: Reverts project state to a previous backup.
* **`aiflow-cli summary`**: Displays a high-level project dashboard.
* **`aiflow-cli fetch-output --retrieval-id <id> --batch <n>`**: Provides instructions to ask the AI for a specific batch of a large AI output.

Run `aiflow-cli [command] --help` for detailed options for each command.

## Basic Workflow Example (Manual AI Interaction)

1.  **Initialize:** `aiflow-cli init` (optionally with a `--template`)
2.  **Define Task & Prepare for AI:**
    `aiflow-cli prepare-input --task-desc "Draft an introduction for the family chore system document." --new-resource "docs/chore_intro.md"`
3.  **Send to AI:** Copy the JSON output from the CLI and paste it into your chat with an AI.
4.  **Receive from AI:** The AI processes the request and provides a JSON response.
5.  **Integrate AI Response:** Copy the AI's JSON response.
    `aiflow-cli integrate-output --dry-run "<PASTED_AI_JSON>"` (Review the dry run)
    `aiflow-cli integrate-output "<PASTED_AI_JSON>"` (Apply changes)
6.  **Review Status:** `aiflow-cli status` or `aiflow-cli summary`
7.  **Iterate:** Add notes, update tasks, or prepare new inputs for the AI.
    `aiflow-cli task note <task_id> -m "Ask AI to make it more kid-friendly."`
    `aiflow-cli prepare-input --task-id <task_id> --resource "docs/chore_intro.md" --task-desc "Revise the intro to be more engaging for children."`

## Configuration Files

* **`aiflow.json`**: The main state and configuration file for your project. Contains project settings, task lists, resource tracking, roadmap, and AI provider settings. Generally managed by the CLI.
* **`.aiflowignore`**: A text file in your project root, similar to `.gitignore`. List file paths or glob patterns (one per line) to exclude from being processed or sent to the AI during `prepare-input`.
* **`aiflow_templates.json`** (or user-specified name): A JSON file defining project templates that can be used with `aiflow-cli init --template <name>`. This allows you to kickstart different types of projects (e.g., coding, document drafting, chore management) with pre-defined tasks, resources, and configurations.

## Future Development (AIFlow v2 Vision)

The AIFlow project is designed with future enhancements in mind:

* **Direct API Integration:** The primary goal for v2 is to integrate directly with AI APIs like Google Gemini. This will automate the copy-pasting of requests and responses.
    * `aiflow-cli prepare-input --send ...`
    * Automated handling of API responses.
* **Interactive AI Dialogue:** Enable more conversational, multi-turn interactions with the AI directly through the CLI.
* **Streaming Responses:** Display AI-generated content (like code or text) as it's being streamed from the API.
* **Hosted Service Model:** The core logic (request preparation, AI interaction services) could potentially be hosted as a local, cross-platform service. The CLI (and future GUIs) would then act as clients to this local AIFlow service, enabling more robust state management and advanced features.
* **Plugin System for AI Providers:** Allow developers to create plugins for different LLM providers beyond an initial Gemini integration.

## Contributing

(Placeholder) AIFlow is currently a conceptual project. If it were open source, contributions would be welcome! This would typically include guidelines for reporting bugs, suggesting features, and submitting pull requests.
