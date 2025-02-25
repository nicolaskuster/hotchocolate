---
title: "Non-Null"
---

Per default all fields on an object type can be either `null` or the specified type.

```sdl
type User {
  name: String
}
```

In the above example `name` can either be `null` or a `String`.

Being nullable does not make sense for every field though. Maybe we have some database constraint which enforces the `name` to never be `null`.
GraphQL allows us to be specific about this, by marking a field as non-null.

```sdl
type User {
  name: String!
}
```

The exclamation mark (`!`) denotes that the field can never be `null`.
This is also enforced by the execution engine. If we were to return a `null` value in the `name` resolver, the execution engine would throw an error. This prevents unexpected `null` values from causing issues in the consuming applications.

<iframe width="560" height="315"
src="https://www.youtube.com/embed/Zx0nvTUfjn4"frameborder="0" 
allowfullscreen></iframe>

# Implicit nullability

Hot Chocolate automatically infers the nullability of the schema type from the nullability of the used CLR type.

[Value types](https://docs.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-types) are non-null per default, unless they have been marked as nullable.

| CLR Type                | Schema Type |
| ----------------------- | ----------- |
| int                     | Int!        |
| int?                    | Int         |
| Nullable&#x3C;int&#x3E; | Int         |

[Reference types](https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/reference-types) are always nullable, unless we have enabled [nullable reference types](https://docs.microsoft.com/dotnet/csharp/nullable-references). With nullable reference types enabled all fields are non-null per default.

We strongly encourage the use of nullable reference types.

# Explicit nullability

We can also be explicit about the nullability of our fields.

<ExampleTabs>
<Annotation>

```csharp
public class Query
{
    [GraphQLNonNullType]
    public Book GetBook()
    {
        return new Book { Title  = "C# in depth", Author = "Jon Skeet" };
    }
}

public class Book
{
    [GraphQLNonNullType]
    public string Title { get; set; }

    public string Author { get; set; }
}
```

</Annotation>
<Code>

```csharp
public class QueryType : ObjectType<Query>
{
    protected override void Configure(IObjectTypeDescriptor<Query> descriptor)
    {
        descriptor
            .Field(f => f.GetBook())
            .Type<NonNullType<BookType>>();
    }
}

public class BookType : ObjectType<Book>
{
    protected override void Configure(IObjectTypeDescriptor<Book> descriptor)
    {
        descriptor
            .Field(f => f.Title)
            .Type<NonNullType<StringType>>();

        descriptor
            .Field(f => f.Author)
            .Type<StringType>();
    }
}
```

</Code>
<Schema>

```sdl
type Book {
  title: String!
  nullableTitle: String
}
```

</Schema>
</ExampleTabs>

The inner type of a list can be made non-null like the following.

<ExampleTabs>
<Annotation>

```csharp
public class Book
{
    [GraphQLType(typeof(ListType<NonNullType<StringType>>))]
    public List<string> Genres { get; set; }
}
```

</Annotation>
<Code>

```csharp
public class BookType : ObjectType<Book>
{
    protected override void Configure(IObjectTypeDescriptor<Book> descriptor)
    {
        descriptor
            .Field(f => f.Genres)
            .Type<ListType<NonNullType<StringType>>>();
    }
}
```

</Code>
<Schema>

```sdl
type Book {
  genres: [String!]
}
```

</Schema>
</ExampleTabs>
