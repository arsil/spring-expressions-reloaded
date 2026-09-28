# Spring Expressions Reloaded

A standalone .NET expression language: the **Spring.NET Expression Language**, taken out of
Spring.NET and developed further. It lets you evaluate small, user-editable expressions such as

```
Customer.Name
Lines.?{Quantity > 10}.!{Product.Price * Quantity}.sum()
Total > 1000 and Customer.Country == 'PL' ? 'priority' : 'normal'
```

against your own .NET objects, at run time.

This fork adds four things on top of the original:

1. **Language additions for a typed, compiled language.** Casts, null-conditional access (`?.`,
   `?[`), typed local variables, and support for your own types' operators and conversions.
2. **Compilation.** Expressions can be compiled to real .NET delegates, not only interpreted.
3. **A strongly typed API.** You say what the input and output types are, and you get back an
   object you can hold on to and call many times.
4. **A sandbox, on by default.** An expression can no longer reach any type in the process just by
   naming it.

> **Status: work in progress.** The engine is usable and heavily tested, but the compiled backend
> is still being extended and the API may change before a first release.

---

## Where it comes from

Spring.NET (the .NET port of the Java Spring Framework) shipped an expression language inside
`Spring.Core`. It is a good language — properties, methods, operators, collections, projections,
selections, lambdas, variables — but it was only available as part of the whole framework, and it
was only ever interpreted.

This repository extracts it into its own library (`SpringExpressions`), with no dependency on the
rest of Spring.NET. The original source is Apache 2.0 licensed; so is this fork.

The language itself is documented in Spring.NET's own reference, chapter 11, "Expression
Evaluation" (the original site is gone; an
[archived copy](https://web.archive.org/web/20240419/http://www.springframework.net/doc-latest/reference/html/expressions.html)
survives). Everything in that chapter still applies, with the differences listed further down.

## A quick taste of the language

```text
Name                                  property access
Name.ToUpper()                        method call
Orders[0].Total                       indexing
Price * Quantity + Shipping           arithmetic
Age >= 18 and Country == 'PL'         logic and comparison
IsVip ? 'gold' : 'standard'           conditional
{1, 2, 3}                             list literal
#{'a' : 1, 'b' : 2}                   map literal
Orders.?{Total > 100}                 selection (filter)
Orders.^{Total > 100}                 first match
Orders.${Total > 100}                 last match
Orders.!{Total}                       projection (map)
Orders.!{Total}.sum()                 collection functions: sum, count, min, max, average,
                                        sort, distinct, reverse, nonNull, orderBy, convert
Orders.orderBy({|a, b| $a.Total - $b.Total})     lambdas
#discount * Total                     variables supplied by the caller
T(System.Math).Max(A, B)              a type, and a static call on it
new System.Text.StringBuilder()       constructors
Total as int                          casts
(int $x = 5; $x + Number)             typed local variables
Author?.Name ?? 'anonymous'           null-conditional access, with a fallback
```

## 1. Language additions

The original language was written for an interpreter, where every value is just an `object` and its
type is only discovered at run time. A compiler works the other way round: it needs to know types
*before* the expression runs. The additions below are mostly about letting an expression say what
it means about types, so that it can be compiled and so that mistakes are caught early.

### Casts

A cast states a type, and it is often the one thing that lets an expression compile. A `#variable`,
an untyped local, or a value read out of a `Dictionary<string, object>` is only known as `object`,
so nothing after it can be compiled until you say what it is.

```text
45.6 as int                                  45 - behaves like a C# cast, so it truncates
Age as long                                  widening
(#discount as decimal) * Total               a #variable is object; the cast makes the rest compile
(Settings['timeout'] as int) + 5             reading from a Dictionary<string, object>
Ints.min() as int                            min() returns an int? (null when empty); ask for an int
3 as System.DayOfWeek                        enum casts
Payload as string[]                          arrays
Items as System.Collections.Generic.List<int>   generic types
Value as T(Acme.Money, Acme.Model)           T(...) for anything else, e.g. assembly-qualified names
typeof(System.DateTime)                      typeof(...) is another spelling of T(...)
```

There is also a prefix form, which reads well at the start of a chain:

```text
as<int>(45.6)
as<string[]>(Tags).Length
as<System.Collections.Generic.List<int>>(Items).Count
```

Both forms mean exactly the same thing, and a cast gives the same answer whether the expression is
compiled or interpreted: C#'s answer. So `45.6 as int` is `45`, a user-defined conversion operator on
your type is honoured, and casting a string to a number (`'45' as int`) is an error, as it is in C#.

A cast is also the way out when the engine cannot choose between method overloads from the static
types alone: `Show(Payload as string)` picks `Show(string)` and compiles.

### Null-conditional access: `?.` and `?[`

Borrowed from C#. If the left side is null, the whole rest of the chain is skipped and the result is
null, instead of an error:

```text
Author?.Name                       null if Author is null
Author?.Greet()                    the method is not called at all when Author is null
Items?[0]?.Title                   works for arrays, lists and dictionaries
Author?.Name ?? 'anonymous'        combined with ?? (which the original language already had)
```

The left side is evaluated only once, and it works on both backends.

### Typed local variables

The original language already had local variables (`$x = 5`), but they always hold an `object`, so
anything done with them needs a cast before it can be compiled. You can now declare the type:

```text
(int $x = 5; $x + Number)
(decimal $net = Total - Discount; $net + $net * VatRate)
(System.Collections.Generic.List<int> $xs = Ints; $xs.Count)
```

- The value is converted to the declared type when the variable is set, using the same implicit
  conversions C# would allow (`int` into `long`, and so on).
- A declared variable always has a value: it starts as that type's default (`0` for an `int`, null
  for a `string`).
- A variable is visible to the whole expression, and a name can be declared only once.

### Your own types' operators and conversions

If your types declare operators, expressions use them, just as C# would. Before, a type could only
take part in arithmetic if it happened to convert to a built-in number.

```csharp
public struct Money
{
    public static Money operator +(Money a, Money b) { ... }
    public static bool operator >(Money a, Money b) { ... }
    public static bool operator <(Money a, Money b) { ... }
    public static bool operator ==(Money a, Money b) { ... }
    // ...
}
```

```text
Price + Shipping              calls Money's own operator +, and the result is a Money
Price + Shipping > Limit      calls Money's own operator >
Price == Budget               calls Money's own operator ==
-Price                        calls Money's own unary minus, if it has one
```

The arithmetic operators, the four comparisons, `==` (with `!=` always meaning "not `==`"), unary
`-` and `+`, and `!` are all supported. The same goes for `DateTime + TimeSpan` and the other
operators .NET's own types declare.

**Implicit conversion operators** are honoured too. A type that converts to a number behaves like
that number:

```csharp
public struct Quantity
{
    public static implicit operator int(Quantity q) { ... }
}
```

```text
Qty * 2                       an int, as C# would give you
Qty > 10                      compared as an int
TakesInt(Qty)                 passed to an int parameter
Count = Qty                   assigned to an int property
Total = Qty                   int, then widened to decimal, as in C#
```

## 2. Compilation

The original engine walks the parsed expression tree every time it is evaluated. This fork adds a
second backend: each node of the tree can also produce a `System.Linq.Expressions` tree, which is
compiled into an ordinary .NET delegate.

A compiled expression, once built, runs at close to the speed of hand-written C#. Measured on one
simple expression:

| | per evaluation |
|---|---|
| compiled, held and reused | **~0.014 µs** |
| interpreted | ~10 µs |

Compiling has an up-front cost (a few hundred microseconds), so it pays off when an expression is
evaluated many times — which is the usual case for rules, filters, bindings and templates.

**Not every expression can be compiled, and that is fine.** The interpreter can handle anything
the compiler can, plus more. By default the engine compiles when it can and quietly interprets when
it cannot. You choose the behaviour with `EvaluationMode`:

| mode | meaning |
|---|---|
| `CompileOrInterpret` | the default — compile if possible, interpret otherwise |
| `MustCompile` | compile, or throw `CompileErrorException` explaining which part could not be compiled |
| `MustInterpret` | never compile |

**Both backends are meant to give the same answer.** A caller normally does not choose which
backend runs, so a difference between them would be a bug the caller cannot defend against. A large
part of the work in this fork went into finding and removing such differences, including test
suites that generate thousands of expressions and compare the two backends against each other. Where
the original behaviour was ambiguous, the fork generally follows C#'s rules.

You can ask how an expression ended up being evaluated:

```csharp
var getter = Expression.ParseGetter<Order, decimal>("Lines.!{Price * Quantity}.sum()");

CompilationStatus status = Expression.GetCompilationStatus(getter);
// status.Kind            -> Compiled or Interpreted
// status.Reason          -> Requested or CompilationRefused
// status.RefusalMessage  -> e.g. "Cannot compile OpLike 'like': ..."
```

## 3. The strongly typed API

The recommended way to use the library. You parse an expression **once**, stating the type it
runs against and the type it returns, and then call it as often as you like.

```csharp
using SpringExpressions;

// Read a value
IGetterExpression<Order, decimal> total =
    Expression.ParseGetter<Order, decimal>("Lines.!{Price * Quantity}.sum()");

decimal value = total.GetValue(order);

// Read with variables supplied by the caller
var discounted = Expression.ParseGetter<Order, decimal>("Total * (1 - #discount)");
decimal price = discounted.GetValue(order, new Dictionary<string, object> { ["discount"] = 0.1m });

// Write a value
ISetterExpression<Order, string> status =
    Expression.ParseSetter<Order, string>("Status");

status.SetValue(order, "Shipped");

// Run an expression for its side effect
IVoidExpression<Order> clear = Expression.ParseVoidExpression<Order>("Lines.Clear()");
clear.Execute(order);

// Insist on compilation, and find out at parse time if that is not possible
var strict = Expression.ParseGetter<Order, bool>("Total > 1000", EvaluationMode.MustCompile);
```

There are also root-less variants — `ParseGetter<TResult>`, `ParseSetter<TValue>`,
`ParseVoidExpression` — for expressions that do not need an object to run against.

Why typed:

- **The compiler needs the types.** It binds members against the declared type of the root, so it
  can only compile what it can see. Measured on this repository's own tests, **84%** of expressions
  compile when the root type is declared, against **under 7%** when it is only known as `object`.
- **Mistakes surface early.** With `MustCompile`, an expression that cannot be compiled is reported
  when you parse it, with a message naming the part that failed — not later, on some evaluation.
- **You keep the parsed object.** Parsing and compiling happen once; each call afterwards is just a
  delegate invocation.

Parsed expressions are safe to share between threads.

## 4. The weakly typed API, and `ExpressionEvaluator`

The original Spring.NET API is still here, so existing code keeps working:

```csharp
IExpression e = Expression.Parse("Customer.Name");
object name = e.GetValue(order);             // context type inferred as Order
e.SetValue(order, "Alice");
```

`Expression.Parse` returns an object that decides, separately for each context type you call it
with, whether to compile or interpret. It works, but it can only compile against the type **the call
site declares** — `GetValue<object>(x)` has nothing to compile against and will interpret. Prefer the
strongly typed API for new code.

### `ExpressionEvaluator` is obsolete

The old one-line helper class, `ExpressionEvaluator`, is marked `[Obsolete]` (a warning, not an
error):

```csharp
// Old: parses the expression again on every single call
object name = ExpressionEvaluator.GetValue(order, "Customer.Name");
ExpressionEvaluator.SetValue(order, "Status", "Shipped");
```

It parses the expression on every call, so nothing can ever be reused. There is no situation in
which it is the better choice: even for a genuine one-off call, the typed API is as fast or faster.

```csharp
// New: parse once, reuse
var name = Expression.ParseGetter<Order, string>("Customer.Name");
string value = name.GetValue(order);

// A genuine one-off, still no slower than the old class
string once = Expression.ParseGetter<Order, string>("Customer.Name", EvaluationMode.MustInterpret)
                        .GetValue(order);
```

While it lasts, `ExpressionEvaluator` interprets by default, exactly as before. It has gained
generic overloads (`GetValue<TRoot>`, `SetValue<TRoot, TValue>`) that let it compile if you
explicitly pass `EvaluationMode.CompileOrInterpret`.

## 5. The sandbox

In the original library an expression could reach **anything in the process**. Nothing stopped a
string like these from running:

```text
new System.Diagnostics.Process()
T(System.IO.File).Delete('C:\important.txt')
'abc'.GetType().Assembly
```

The last one is the interesting case: it names no dangerous type at all, it just walks from an
ordinary string to the reflection API. Worse, when resolving a type name the original engine would
search every loaded assembly and could even **load a new assembly from disk** because an expression
mentioned it.

That is a real problem whenever expression text comes from users, configuration, a database, or
anywhere else you do not fully control. This fork adds a sandbox, and **it is on by default**.

### The rule in three lines

- An expression may **name** only types the policy lists — in `T(X)`, `new X()`, `x as X`,
  `x is T(X)`, or a static call.
- An expression may **reach** anything your own objects expose — properties, fields and methods of
  the objects you pass in — unless the policy forbids it.
- Some types are **forbidden** outright, and are blocked however an expression gets to them.

The idea is that *you* decide what an expression can see, by what you put in the object you give
it. An expression can use your `Order`, its `Customer` and their properties freely. What it cannot do
is conjure up a type you never gave it.

### What the default policy allows

`SandboxPolicy.Default` starts out as `SandboxPolicy.Restricted`, the built-in catalog:

| category | types |
|---|---|
| **allowed, all members** | the primitives (`bool`, `char`, `string`, all integer and floating-point types, `decimal`), `DateTime`, `TimeSpan`, `DateTimeOffset`, `Guid`, `Math`, `Convert`, `Version`, `Uri`, `StringBuilder`, `Nullable<T>`, the common collections (`List<T>`, `Dictionary<K,V>`, `HashSet<T>`, `ArrayList`, `Hashtable`, and their interfaces), calendar and number/date format info, **any enum** |
| **allowed, some members** | `System.Type` (descriptive properties such as `Name`, `FullName`, `IsEnum` — not `Assembly`), `object` (`ToString`, `Equals`, `GetHashCode`, `GetType`), `Array` (not its factory methods), `CultureInfo` (cannot change the current culture), `Environment` (read-only facts such as `NewLine`, `ProcessorCount`, `OSVersion` — not `Exit`, not machine or user names) |
| **forbidden** | reflection (`Assembly`, `Module`, `MemberInfo` and its subclasses), `AppDomain`, `Activator`, `GC`, `Delegate`, and the effect types: `Stream`, `TextReader`, `TextWriter`, `FileSystemInfo`, `Process`, `Thread`, `Socket` |
| **not listed** | everything else — cannot be named, but can be used if one of your objects hands it out |

Forbidding a type also forbids everything derived from it, so forbidding `Stream` covers
`FileStream`, `MemoryStream` and friends.

### Allowing your own types

If your expressions construct, cast to, or call statics on your own types, add them to the policy
once at startup:

```csharp
SandboxPolicy.Default = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
    .AllowAllMembersOf<Money>()                 // one type, every member
    .Allow<PriceCalculator>("Calculate")        // one type, listed members only
    .AllowAssemblyOf<Order>()                   // every type in your model assembly
    .Build();
```

Or finer grained:

```csharp
var policy = SandboxPolicy.NewBasedOn(SandboxPolicy.Restricted)
    .AllowAllMembersOf<Customer>()
    .ExceptMethod<Customer>("Delete")                   // callable everywhere except this
    .ExceptPropertyOrFieldWrite<Customer>("Id")         // readable, not writable
    .Forbid<AuditLog>()                                 // never reachable, however obtained
    .Build();
```

A policy can also be given to a single expression rather than set process-wide:

```csharp
var e = Expression.ParseGetter<Order, decimal>("Total", sandbox: policy);
```

A policy is immutable and is fixed when the expression is parsed; changing `SandboxPolicy.Default`
later only affects expressions parsed afterwards.

### When something is denied

A denied expression throws `SandboxViolationException`, which tells you the type (`DeniedType`) and,
for a member, its name (`DeniedMember`). It is deliberately **not** a compilation error: the engine
never quietly falls back to the interpreter to get around a denial.

### Turning it off

If you trust every expression you evaluate and want the old behaviour back:

```csharp
SandboxPolicy.Default = SandboxPolicy.DangerouslyAllowEverything;
```

It is named so that nobody types it by accident and a reviewer can search for it.

## 6. Other changes worth knowing

**Smaller language additions**

- **`params` arrays and optional parameters** work in method and constructor calls.
- **A char can be compared with a one-letter string**: `Letter == 'A'`.
- A **nullable** value's members are reachable directly: `ShippedOn.Year` where `ShippedOn` is a
  `DateTime?`.

**Behaviour tightened** — things that used to "work" by accident now raise an error:

- No "truthiness": `45 ? a : b` and `!4.5` are errors, not coerced to booleans. (`null` still
  counts as false.)
- An enum compared with a string must name one of its members: `Type == 'One'` is fine, `Type == '0'`
  is an error.
- `and`, `or` and `??` short-circuit again, and every operand is evaluated exactly once.

**Breaking changes for Spring.NET users**

- Collection operators (`+`, `*`, `-` on sets and lists) return the standard `HashSet<T>` /
  `Dictionary<K,V>` instead of Spring's own `HybridSet` / `Hashtable`.
- List literals and `sort()`, `distinct()`, `reverse()`, projections, selections return `List<T>`
  instead of `ArrayList`; `convert(T)` returns a `List<T>` instead of an array.
- `ICollectionProcessor.Process` takes an `IEnumerable` instead of an `ICollection`.
- Parsed expressions are no longer serializable.
- A node built by hand must be wrapped with `Expression.Wrap(node)` before it can be evaluated.
- The sandbox is on: expressions that name your own types need those types added to the policy
  (usually one line — see above).

The repository contains a copy of the original Spring.NET tests
(`SpringExpressionsLegacyTests`), changed only where this library forced a change. It is a
practical measure of what upgrading costs.

## Supported frameworks

The library targets **.NET Framework 4.0**, **.NET Framework 4.7.2**, **.NET Standard 2.0** and
**.NET 10**. It is tested on net40, net472, netcoreapp2.1, net8.0 and net10.0.

## Building and testing

```powershell
dotnet build SpringExpressions.sln

dotnet test SpringExpressionsTests/SpringExpressionsTests.csproj -f net10.0
dotnet test SpringExpressionsLegacyTests/SpringExpressionsLegacyTests.csproj -f net10.0

# .NET Framework 4.0 has no dotnet test host; use the script
.\run-net40-tests.ps1
```

The parser is generated from `SpringExpressions/Expressions/Parser/Expression.g` with ANTLR 2.7.6
(included). If you change the grammar, run `antlr_generate.bat` from that folder and commit the
regenerated files — the build does not do it for you.

## License

Apache License 2.0, as the original Spring.NET code it is derived from.
