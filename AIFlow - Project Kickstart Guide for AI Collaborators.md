# AIFlow: Project Kickstart Guide for AI Collaborators

## 1. Introduction to AIFlow

Welcome! You are about to collaborate on a project managed by **AIFlow**, a command-line interface (CLI) tool designed to structure and facilitate iterative development between human users and AI assistants like yourself.

Purpose of AIFlow:

AIFlow helps manage tasks, associated project resources (like files, URLs, or text snippets), and the exchange of information between a human user and an AI. It aims to make the collaborative process more organized, traceable, and efficient, especially for projects that involve generating or modifying content, code, or plans.

Your Role:

As an AI collaborator in an AIFlow-managed project, you will receive structured requests from the human user (via the aiflow-cli tool). Your primary role is to process these requests, perform the described tasks (e.g., writing code, generating text, analyzing data, planning steps), and provide your output in a specific structured JSON format that AIFlow can understand and integrate.

## 2. Core AIFlow Concepts

- **`aiflow.json`:** The central metadata file for an AIFlow project. It tracks project configuration, tasks, resources, and their statuses. You generally won't interact with this file directly, but its contents inform the requests you receive.

- **Tasks (`AIFlowTask`):** The fundamental units of work. Each task has an ID, description, status (e.g., "todo", "in_progress", "pending_ai_processing"), assigned resources, and potentially Agile metadata like type, priority, and labels.

- **Resources (`AIFlowResource`):** These are the assets associated with tasks. They can be local files (e.g., source code, documents), URLs, or text snippets. You might be asked to create, update, or analyze these resources.

- **Human Request Package:** When a human user wants you to work on a task, they will use `aiflow-cli prepare-input`. This command generates a JSON payload that the human will then copy and paste to you. This is your primary input.

- **AI Output Package:** After you complete your work, you need to structure your response as a specific JSON payload. The human user will copy this JSON from you and use `aiflow-cli integrate-output` to process it.

## 3. The Workflow (Your Interaction Points)

1. **Receiving a Request:**
   
   - The human user will provide you with a JSON payload, typically wrapped in a `humanRequest` object.
   
   - **Key fields to look for in the request:**
     
     - `taskId`: The ID of the task you need to work on.
     
     - `taskDescription`: Detailed instructions for what you need to do.
     
     - `humanRequestGroupId`, `partNumber`, `totalParts`: If present, this indicates the request is part of a larger, multi-part submission from the human (e.g., for a very large initial set of files). You should expect `totalParts` number of messages for this `humanRequestGroupId` before you have the complete context for the task. Acknowledge receipt of each part and wait for all parts if indicated.
     
     - `filesToProcess`: (Usually in Part 1 of a request) An array of resources. Each object will have:
       
       - `path`: The relative path or identifier of the resource.
       
       - `action`: What to do with it (e.g., "update" an existing resource, "create" a new one).
       
       - `hash`: (For "update") The hash of the resource content the human is providing.
       
       - `contentBase64`: (For small resources, especially local files) The Base64 encoded content of the resource. You'll need to decode this.
       
       - `contentDetail`: May indicate if content is provided in a subsequent part (e.g., "provided_in_part_X") or if it's a non-file resource type (e.g., "type:URL").
     
     - `fileData`: (In subsequent parts of a multi-part request) An array providing the `contentBase64` for resources that were too large for the initial part. Each object will have `path` and `contentBase64`.
   
   - **Your Action:** Carefully parse this JSON. Understand the task description and identify all associated resources and their content.

2. **Processing the Task:**
   
   - Perform the work described in `taskDescription`, using any provided resource content.
   
   - This might involve writing code, drafting documents, analyzing data, answering
