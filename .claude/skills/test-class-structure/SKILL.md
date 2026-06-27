---
name: test-class-structure
description: "Use when writing, extending, or reviewing test classes in C# projects. Ensures test classes stay small and focused by splitting on topic boundaries."
---

# Test Class Structure

## Purpose
Keep test classes small and topic-focused so corrections remain minimal and context consumption stays low.

## Rules

1. Do not put all tests for one class into a single test class once topics and method counts justify splitting (see rule 4).
2. Split by topic: each test class covers exactly one concern of the class under test.
3. Naming: `{TestedClass}Tests_{Topic}` — e.g. `CustomerTests_Properties`, `CustomerTests_PaymentMethods`.
4. Split threshold: create a new test class when a topic reaches 5+ test methods, or when it requires different mocks/fixtures than other groups.
5. One test method tests exactly one behavior — no multi-scenario asserts in a single method.
6. Method naming: `MethodName_Scenario_ExpectedOutcome` — e.g. `AddPaymentMethod_WithDuplicateIban_ThrowsInvalidOperationException`.
7. The same split principle applies to integration and E2E tests: group by flow segment (e.g. `OrderFlow_PlacementTests`, `OrderFlow_PaymentTests`).
8. Each E2E test class covers its topic completely — happy path **and** error cases (misconfiguration, invalid input, unmet preconditions, downstream failures) belong in the same class, not in separate files.

## Topic Examples for a `Customer` Class

| Topic | Class name |
|---|---|
| Property validation | `CustomerTests_Properties` |
| Payment methods | `CustomerTests_PaymentMethods` |
| Mail settings | `CustomerTests_MailSettings` |
| Lifecycle (create/activate/delete) | `CustomerTests_Lifecycle` |

## When Adding Methods to Existing Test Classes

- Only add to a class whose topic matches the new scenario.
- If no existing class fits, create a new one — do not stretch a class to cover an adjacent topic.
