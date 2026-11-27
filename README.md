# GarmentERP - Production Garment & Retail Clothing POS & Enterprise ERP

Enterprise-grade Garment & Clothing Store Point-of-Sale (POS) and Retail ERP system built with **.NET 10**, **ASP.NET Core 10 Web API**, **PostgreSQL 16**, **SQLite** (offline terminal), **Entity Framework Core**, **SignalR**, and **WPF Desktop**.

---

## ⚡ Quick CLI Control (`posf`)

The repository includes a unified management utility `posf` (`posf.cmd`, `posf.bat`, and `posf.ps1`):

```bash
# Launch Interactive Menu
posf

# Start Components Directly
posf api           # Start ASP.NET Core 10 Web API (port 5000)
posf desktop       # Launch WPF Desktop POS Terminal
posf all           # Launch both Server & POS Terminal
posf test          # Run all 22 automated Unit Tests
posf build         # Build entire solution in Release mode
posf publish       # Publish standalone Windows x64 executable
posf docker        # Spin up PostgreSQL 16 + Web API via Docker Compose
posf docker-down   # Tear down Docker Compose containers
posf status        # Inspect server ports & connection status
posf help          # Display command documentation
```

---

## 🏢 Core Business Capabilities
1. **Garment Segments Supported:** Men's, Women's, Kids' clothing, Footwear, and Fashion Accessories.
2. **Store Formats:** Small boutiques, single-counter garment shops, multi-counter fashion outlets, and multi-branch retail chains.
3. **Product Hierarchy:** Parent Product and Variant Matrix (e.g., 5 Sizes × 5 Colors = 25 distinct variants, each with unique SKU, EAN-13 Barcode, purchase price, MRP, and independent inventory tracking).
4. **GST Apparel Slabs (India):** Dynamic calculation adhering to apparel tax rules:
   - Taxable value $\le ₹1,000 \rightarrow$ **5% GST** (2.5% CGST + 2.5% SGST or 5% IGST)
   - Taxable value $> ₹1,000 \rightarrow$ **12% GST** (6% CGST + 6% SGST or 12% IGST)
   - Mathematical invoice round-off to nearest whole rupee with double-entry ledger adjustment.

---

## 🏗️ Technical Architecture

```
GarmentERP.slnx
├── src/
│   ├── GarmentERP.Domain          # Pure domain entities, business logic, GST rules, matrix generator
│   ├── GarmentERP.Shared          # DTOs, Enums, License contracts, Telemetry payload models
│   ├── GarmentERP.Application     # Interfaces & Application service contracts
│   ├── GarmentERP.Infrastructure  # EF Core DbContexts (PostgreSQL & SQLite), DatabaseSeeder, Service Implementations
│   ├── GarmentERP.SyncService     # DataSync Engine (Outbox/Inbox queue with PENDING, SYNCING, DONE, FAILED)
│   ├── GarmentERP.Reporting       # ESC/POS (58mm/80mm) raw byte generation & A4 Tax Invoice HTML builder
│   ├── GarmentERP.Api             # ASP.NET Core 10 Web API, SignalR Hub, Controllers, Health checks
│   └── GarmentERP.Desktop         # Modern WPF Desktop POS application with MVVM, split tender, and hotkeys
└── tests/
    └── GarmentERP.Tests           # Comprehensive unit tests for GST, billing, returns, matrix, accounting, sync
```

---

## 🔄 POSRES DataSync System (`PENDING`, `SYNCING`, `DONE`, `FAILED`)

The terminal operates an offline-first Outbox/Inbox synchronization model replicating the robust POSRES sync workflow:

1. **Transactional Outbox Queuing:**
   - Any transaction created at the offline counter (Sale, Return, Shift Closing, Stock Adjustment) is immediately committed to the local SQLite database.
   - A corresponding `SyncOutbox` event is committed in the same database transaction with status **`PENDING`**.
2. **Background Sync Worker (`SyncEngine`):**
   - Polls for records with `Status == SyncStatus.PENDING` or `SyncStatus.FAILED` (within max retry count).
   - Marks batch as **`SYNCING`**.
   - Dispatches payload over HTTP POST to `/api/v1/sync/push` on the store server or cloud.
   - If server confirms ingestion: status transitions to **`DONE`** with `ProcessedAtUtc` timestamp.
   - If network times out or server is unreachable: status transitions to **`FAILED`**, retry counter increments with exponential backoff, and terminal continues offline billing uninterrupted.
3. **SignalR Realtime Board:**
   - `/hubs/sync` pushes live sync queue metrics (`PendingCount`, `DoneCount`, `FailedCount`) to connected terminal headers.
4. **Idempotent Ingestion:**
   - Server registers event in `SyncInbox` keyed by unique `EventId`. Duplicate events are skipped safely.

---

## 🔐 Licensing System & Hardware Lock (`https://license.abhishekmahant.in`)
- **Primary Server:** `https://license.abhishekmahant.in`
- **Fallback Server:** `https://pos-license-server-4xnq.onrender.com`
- **Endpoints:**
  - `POST /api/license/verify`: Remote license validation, store binding, and heartbeat.
  - `POST /api/license/exit`: Cleans up active sessions on application exit.
- **Machine Fingerprint (MPOS ID):** Generated in the format `MPOS-XXXX-XXXX-XXXX-XXXX` from CPU, motherboard serial, machine name, and OS cryptography GUID.
- **Offline Resiliency:** When the online license server is unreachable, the terminal operates seamlessly under the **15-day offline grace period**.

---

## ✈️ Real-Time Telegram Bot Integration
Automated instant alerts sent directly to the store owner's Telegram chat:
- **Default Bot Token:** `9008744051:AAEhTj6Wx0Q-ELk0ig-lgBOg6V7XoccodjA`
- **Default Chat ID:** `8168115314`
- **Telegram Alert Triggers:**
  - 🟢 **App Startup:** Instant notification when any billing counter launches.
  - 🧾 **Live Bill Completed:** Sent on every finalized invoice with invoice number, items count, grand total (₹), payment mode, and customer details.
  - 📊 **Cashier Shift Z-Report:** Full day-end summary with cash and digital collection.
  - 🚨 **DataSync Error:** Alert if LAN or cloud sync encounters an error or network drop.
  - ⚠️ **Telemetry Crash Alert:** Unhandled exceptions and crash traces sent to Telegram and logged to the central license server.
  - 🔔 **Test Alert:** Interactive button in Desktop `⚙️ Settings` to test connection anytime.

---

## 📡 Error Telemetry Reporting to License Server
- **Client Crash & Exception Reporting:** Unhandled exceptions in the Desktop POS or Sync Engine trigger `IErrorTelemetryReporter`.
- **Payload:**
  - Machine Fingerprint, Store Name, App Version, OS Version
  - Exception Type, Message, Stack Trace, Context Module, Severity (`Warning`, `Error`, `Critical`).
- **Telemetry Endpoint:** POST to `/api/v1/license/telemetry/report`.
- **Offline Resiliency:** Telemetry reports are spooled in local storage and dispatched when internet connectivity resumes.

---

## 💻 POS Desktop Terminal Hotkeys & Features
- **F1:** Search Products by SKU, Name, Barcode, Size, or Color.
- **F6:** Hold Current Bill (saves active cart into memory/local DB and clears register for next customer).
- **F7:** Resume Held Bill.
- **F9:** Open Split Payment Modal (Cash, Card, UPI Dynamic QR with NPCI URI string, Store Credit, Loyalty Points).
- **F10:** Quick Cash Tender (exact amount paid and instant bill finalization).
- **F11:** Day-End Shift Closing & Z-Report generation.
- **F12:** Reprint Last Invoice.
- **ESC:** Cancel active search, modal, or reset cart.
- **Printing Options:**
  - Direct 58mm / 80mm ESC/POS thermal printer output (`ESC @`, `ESC p` drawer kick, `GS V` cut).
  - High-resolution A4 Tax Invoice with GST HSN code breakdown and terms.

---

## 📊 Modules Implemented (All 28 Modules)

| # | Module | Implementation Highlights |
|---|---|---|
| 1 | **Dashboard** | Realtime KPI cards (Sales, Invoices, Items, Cash/Digital, Low Stock, Active Shifts) |
| 2 | **POS Billing** | Fast barcode scanner listener, dynamic apparel GST slabs, split tender, discount engine |
| 3 | **Product Management** | Parent product definition, department hierarchy, category, brand, HSN codes |
| 4 | **Size & Color Management** | Apparel sizes (XS-5XL, 28-44) and colors with hex codes |
| 5 | **Barcode Management** | Auto-generating EAN-13 barcodes with checksum digits per variant |
| 6 | **Purchase Management** | Supplier POs, purchase invoices, landed cost calculation, auto-inventory intake |
| 7 | **Supplier Management** | Supplier ledger, GSTIN, payment terms, and outstanding balances |
| 8 | **Inventory Management** | Multi-location stock tracking, low stock alerts, stock adjustment vouchers |
| 9 | **Warehouse Management** | Central distribution centers, store back-room storage zones |
| 10 | **Stock Transfer** | Inter-branch and warehouse-to-store transfer manifests with dispatch and receipt |
| 11 | **Customer Management** | Phone number quick lookup, purchase history, loyalty membership |
| 12 | **Loyalty Management** | Configurable point accrual rate (e.g. 1 pt per ₹100), redemption during checkout |
| 13 | **Return & Exchange** | Original invoice lookup, restocking fee, reason codes, credit note issuance |
| 14 | **Discount & Promotion** | Percentage off, flat amount, Buy-X-Get-Y (BOGO), minimum spend conditions |
| 15 | **GST Management** | Dual-tier apparel rate engine (5% vs 12%), GSTR-1 HSN summary export |
| 16 | **Accounting** | Double-entry journal vouchers, Chart of Accounts, Trial Balance, Round-Off ledger |
| 17 | **Expense Management** | Store utility, rent, tea/refreshment petty cash expense vouchers |
| 18 | **Staff Management** | Staff employee profiles, PIN-based cashier login, shift assignment |
| 19 | **RBAC** | Strict permissions (`Admin`, `Manager`, `Cashier`, `Auditor`) |
| 20 | **Sales Reports** | Date-range, cashier-wise, payment-method-wise, product-wise sales summaries |
| 21 | **Purchase Reports** | Supplier purchase registers, tax paid on inward supplies |
| 22 | **Stock Reports** | Inventory valuation (FIFO / weighted average), reorder recommendations |
| 23 | **Profit & Loss Reports** | Gross margin per category, net profit after store expenses |
| 24 | **Multi-Branch Management** | Branch registration, multi-store price lists, consolidated reporting |
| 25 | **Data Synchronization** | Offline SQLite Outbox/Inbox with `PENDING`, `SYNCING`, `DONE`, `FAILED` queues |
| 26 | **Backup & Restore** | Database snapshot export, SHA-256 integrity verification |
| 27 | **Hardware Integration** | 58mm/80mm ESC/POS thermal printers, cash drawer kick pulse, USB/Bluetooth scanners |
| 28 | **System Settings** | Store GSTIN, address, thermal receipt footer note, currency formatting |

---

## 🧪 Unit Testing Suite
All 22 automated tests pass with 100% success:
- `VariantMatrixGeneratorTests` (25-variant generation from 5 sizes × 5 colors)
- `GstCalculatorTests` (Apparel 5% and 12% thresholds, CGST+SGST vs IGST, invoice round-off)
- `SplitPaymentAndUpiTests` (Change calculation, NPCI UPI string generation)
- `ReceiptPrintingTests` (ESC/POS raw bytes, drawer kick, A4 HTML invoice)
- `PromotionAndReportingTests` (BOGO, percentage discounts, profit and loss computation)
- `PosBillingTests` (Complete sale checkout, stock decrement, Outbox queuing)
- `PurchasesAndInventoryIntakeTests` (Inward stock intake, supplier balance update)
- `StockTransferWorkflowTests` (Dispatch from warehouse, receive at store counter)
- `LoyaltyAndReturnTests` (Loyalty accrual/redemption, return workflow, credit note generation)
- `GstAndAccountingTests` (GSTR-1 summary generation, balanced journal vouchers)
- `StaffAuthAndBackupTests` (PBKDF2/SHA-256 password hashing, RBAC, database backup snapshot)

```bash
dotnet test GarmentERP.slnx --logger "console;verbosity=normal"
```

---

## 🚀 Deployment Guide

### Option 1: Docker Compose (Local Ubuntu Server / On-Premise)
Run the entire production stack (PostgreSQL 16 + ASP.NET Core 10 Web API):

```bash
# Clone and enter project directory
cd /path/to/POSFASHION

# Launch containers
docker compose up -d --build

# Check status
docker compose ps
docker compose logs -f api
```

The API will be accessible at:
- **Web API & Swagger:** `http://localhost:5000/swagger`
- **SignalR Hub:** `http://localhost:5000/hubs/sync`
- **Health Check:** `http://localhost:5000/health`

### Option 2: One-Click Render Cloud Deployment
1. Link your Git repository to **Render.com**.
2. Render automatically detects `render.yaml`.
3. The blueprint provisions:
   - Managed **PostgreSQL 16** database with daily backups.
   - Containerized **ASP.NET Core 10** Web API with automatic SSL.
4. Database migrations and seeding run automatically on first boot.

### Option 3: Desktop POS Client Setup (Billing Counter)
1. Build the Desktop application:
   ```bash
   dotnet publish src/GarmentERP.Desktop/GarmentERP.Desktop.csproj -c Release -r win-x64 --self-contained
   ```
2. Distribute the published folder to your counter PCs.
3. Configure `appsettings.json` in the counter folder:
   ```json
   {
     "ApiBaseUrl": "http://192.168.1.100:5000",
     "BranchCode": "BR001",
     "TerminalCode": "POS-01",
     "PrinterName": "POS-80C",
     "PrinterPaperWidth": 80
   }
   ```
4. Launch `GarmentERP.Desktop.exe`. The app will connect to the local store server API, verify the license, seed local SQLite offline tables, and enable keyboard-driven billing.
