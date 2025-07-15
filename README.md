# SharpCore Kernel Documentation Structure (English)

This document outlines the structure and purpose of the main files inside the **SharpCore Kernel** and its related modules: `Staging`, `Logging`, and `Core` logic. Each section explains the responsibility of each file or module to assist future contributors and users.

---

## 1. Kernel Core Modules

### 📁 `Kernel_Init`

#### `Program.cs`

* **Role:** Entry point for standalone kernel execution.
* **Description:** Launches the kernel manually without CLI or GUI.
* **Use case:** Direct execution for testing or embedded systems.

#### `SCKernel_init.cs`

* **Role:** High-level kernel runner.
* **Methods:**

  * `Run(string payloadPath, string protocol, string adapter)`: Fetches the protocol handler, starts the bridge, sends JSON payload.

---

### 📁 `Kernel_Factory`

#### `IProtocol.cs`

* **Role:** Interface for  protocol creation.
* **Implements:** CreateBridge();
* **Implemented by:** ProtocolFactory.cs


#### `ProtocolFactory.cs`

* **Role:** Provides protocol handlers dynamically.
* **Pattern:** Factory Pattern with `switch` or `map` lookup.
* **Example protocols:** NamedPipe, gRPC, UnixSocket.

---

### 📁 `NamedPipes_utils`

#### `IBridgeConnection.cs`

* **Role:** Interface for any communication bridge.
* **Methods:**

  * `Start()` – Initiates the protocol listener.
  * `Send(string)` – Sends a string to the connected target.

#### `NamedPipeBridgeConnection.cs`

* **Role:** Named Pipe implementation of `IBridgeConnection` and `IProtocol`.
* **Responsibilities:**

  * Handles IPC via named pipes.
  * Deserializes payloads and manages staging.

#### `PayloadMapping.cs`

* **Role:** Optional helper to transform JSON to specific structures.
* **Not always necessary** unless working with complex nested payloads.

#### `FinishAdapterLayer.cs`

* **Role:** Final entry point for mod adapters.
* **Implements:** `IInstructionAdapter`
* **Communicates with:** Java or other runtime.

#### `Protocols/NamedPipeProtocol.cs`

* **Role:** Named pipe handler.
* **Returned by:** ProtocolFactory.
* **Creates:** instances of `NamedPipeBridgeConnection`.

---

## 2. 📁 `Staging` Module

#### `DTO/MSharpInstruction.cs`

* **Role:** Payload structure.
* **Properties:**

  * `Tipo`, `Entidad`, and other mod-related fields.
* **Serialized from JSON** inside the bridge.

#### `StagingManager.cs`

* **Role:** Core of the staging system.
* **Functions:**

  * `MSadd()`, `MScommit()`, `MSrevert()`
* **Handles:** rollbacks, commits and validation flows.

#### `IInstructionAdapter.cs`

* **Role:** Interface to talk with external platforms.
* **Implemented by:** FinishAdapterLayer (and others).

#### `IInstructionValidator.cs`

* **Role:** Validates the payload before staging.
* **Called by:** Kernel before commit or revert.

#### `InstructionProcessor.cs`

* **Role:** Applies staging logic.
* **Could call:** scripting runtimes, native APIs, etc.

#### `ResultValidator.cs`

* **Role:** Final check before committing.
* **Optional, but can prevent runtime crashes.**

---

## 3. 📁 `Logging`

#### `KernelLog.cs`

* **Role:** Static logger used across core and CLI.
* **Backend:** Serilog.
* **Methods:**

  * `Panic(string, Exception?)` – Logs to file and crashes.
  * `Info(string)` – Info logs.
  * `Warn(string)` – Warnings.
  * `Debug(string)` – Verbose logs.

#### 🔧 Suggested addition:

* Expose `.Init(logPath)` method to dynamically set log directories depending on whether it's running from CLI or kernel.

---

#### License: GPL 3.0
#### Contact: 
* `<ivanrwcm25@gmail.com>`


