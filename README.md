# WalletAPI
A RESTful wallet service built with **ASP.NET Core 9**, providing secure user authentication, wallet management, and real-time financial event processing.

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)

---

## Overview

WalletApi is a backend service that allows users to register, authenticate, manage a digital wallet, transfer funds to other users, and view a full paginated transaction history. The system focuses on getting money transactions right — transfers are atomic, every action is permanently recorded, and suspicious activity is flagged automatically.

---

## Features

### Functional Requirements

| # | Capability | Description |
|---|---|---|
| F-1 | **User Accounts** | Users can register with an email and password, authenticate via JWT, and refresh sessions using a rotating refresh token. |
| F-2 | **Wallet Management** | Every registered user is provisioned a wallet automatically. Users can view their current balance and wallet status. |
| F-3 | **Top-Up** | Users can credit their wallet with funds. Each top-up produces a permanent transaction record. |
| F-4 | **Fund Transfer** | Users can transfer funds to another wallet atomically. The debit and credit either both complete or neither does — partial completion is not possible. |
| F-5 | **Transaction History** | Users can retrieve a paginated, reverse-chronological list of all transactions associated with their wallet. |
| F-6 | **Financial Event Notifications** | The system publishes events on top-up and transfer completion. Downstream consumers deliver notifications to the relevant users. |
| F-7 | **Fraud Detection** | The system automatically flags suspicious financial activity for review via a dedicated event consumer. |

### Non-Functional Requirements

| # | Requirement | Detail |
|---|---|---|
| N-1 | **Atomicity** | A transfer must never partially complete. Both the sender debit and recipient credit are committed in a single database transaction or rolled back entirely. |
| N-2 | **Idempotency** | Duplicate requests must not result in duplicate charges. Each transaction is assigned a unique reference; re-submitted operations are detected and rejected. |
| N-3 | **Latency** | Read operations (balance, transaction history) target low latency. Write operations (top-up, transfer) accept slightly higher latency in exchange for consistency and durability guarantees. |
| N-4 | **Audit Trail** | Every financial event is permanently logged. No transaction record is ever deleted or mutated after creation. Structured logs via Serilog capture the full lifecycle of every request. |
| N-5 | **Concurrency Safety** | Concurrent transfers on the same wallet pair are serialized using pessimistic row-level locking (`SELECT FOR UPDATE`) with a consistent lock-ordering strategy to prevent deadlocks. |
| N-6 | **Scale Target** | Designed to serve approximately **1 million registered users** and **500,000 daily active users**, with a sustained peak write throughput of **~50 transfers per second**. |

---

## Architecture

### High-Level System Diagram

```
  Mobile / Web
       │
       ▼
  Load Balancer
       │
       ▼
  ┌──────────────────────────────────────┐
  │          Wallet API Cluster          │
  │    N stateless ASP.NET Core pods     │
  └──────────┬──────────────┬────────────┘
             │              │            │
             ▼              ▼            ▼
  ┌──────────────┐  ┌─────────────┐  ┌───────────────────┐
  │ Auth Service │  │ Redis Cache │  │ PostgreSQL Primary │
  │              │  │             │  │                   │
  │ JWT          │  │ Sessions ·  │  │ Wallets ·         │
  │ Refresh      │  │ Idempotency │  │ Transactions      │
  │ tokens       │  │ keys        │  └────────┬──────────┘
  └──────────────┘  └──────┬──────┘           │ replication
                           │ publishes        ▼
                           ▼         ┌───────────────────┐
                    ┌─────────────┐  │ PostgreSQL Replica │
                    │  RabbitMQ   │  │                   │
                    │             │  │ Read-only · tx    │
                    │   wallet    │  │ history queries   │
                    │   .events   │  └───────────────────┘
                    │   exchange  │
                    └──────┬──────┘
                           │ consumes
           ┌───────────────┼───────────────┐
           ▼               ▼               ▼
  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
  │Notification │  │    Fraud    │  │    Audit    │
  │   Worker    │  │   Worker    │  │   Worker    │
  │             │  │             │  │             │
  │ Email · SMS │  │  Pattern    │  │  Immutable  │
  │ · push      │  │  detection  │  │  audit log  │
  └─────────────┘  └─────────────┘  └─────────────┘
```

### Component Responsibilities

| Component | Role |
|---|---|
| **Load Balancer** (Nginx / AWS ALB) | Terminates TLS, distributes traffic across API pods, and forwards `X-Forwarded-For` and `X-Forwarded-Proto` headers. |
| **Wallet API Cluster** | N stateless ASP.NET Core 8 pods. Stateless design allows horizontal scaling without session affinity. |
| **Auth Service** | Issues short-lived JWT access tokens and rotating refresh tokens. Refresh tokens are BCrypt-hashed before persistence. |
| **Redis Cache** | Stores session data and idempotency keys. Prevents duplicate financial operations from being processed more than once. |
| **PostgreSQL Primary** | Source of truth for all wallet balances and transaction records. All writes target this node. Transfers use `SELECT FOR UPDATE` row-level locking with GUID-ordered acquisition to prevent deadlocks. |
| **PostgreSQL Replica** | Read-only follower. Paginated transaction history queries are routed here to offload the primary and keep read latency low. |
| **RabbitMQ** (`wallet.events` exchange) | Decouples the API from downstream workers. MassTransit publishes events with exponential back-off retry on failure. |
| **Notification Worker** | Consumes transfer and top-up events; delivers email, SMS, and push notifications to the relevant users. |
| **Fraud Worker** | Consumes financial events; applies pattern detection rules and flags suspicious activity for review. |
| **Audit Worker** | Consumes all financial events; appends immutable records to a permanent audit log. |
