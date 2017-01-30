
# Task Conditions

## Types

### Boolean
`true` or `false` (ordinal case insensitive)

### Number
Starts with `-` `.` or `0-9`. Internally parses into .Net `Decimal` type using invariant culture.

Cannot contain `,` since it is a separator.

### Version
Starts with a number and contains two or three `.`. Internall parses into .Net `Version` type.

Note, only one `.` is present, then the value would be parsed as a number.

### String
Single-quoted, e.g. 'this is a string' or ''

Literal single-quote escaped by two single quotes, e.g. 'hey y''all'

### Dictionary (String =\> Any)
Pre-defined dictionary objects are available depending on the context.

Within the agent context, the `variables` dictionary object is available. The variables dictionary contains String values only.

Within the server context, the routing `capabilities` dictionary object is available. The capabilities dictionary contains String values only.

TODO: need more details from Patrick:

Within the server context, a complex orchestration-state object is available (String=\>Any and may contain nested objects). Note, `Any` may be one of any supported type: Boolean, Number, Version, String, or Dictionary(String=\>Any)

#### Syntax to access values
Two syntaxes are supported for accessing the values within a dictionary.
* Indexer syntax - variables['Agent.JobStatus']
* Property dereference syntax - variables.MyFancyVariable
 - In order to use the property dereference syntax, the property name must adhere to the regex `^[a-zA-Z_][a-zA-Z0-9_]*$`

Examples for complex objects:
* Chaining accessors: `SomeComplexObject.FirstLevelObject.['SecondLevelObject'].ThirdLevelObject`
* Nested evaluation: `SomeComplexObject[AnotherObject['SomeProperty']]`

#### Accessor rules
* When an accessor (indexer or property-dereference) is applied against a dictionary object and the key does not exist, null is returned.
* When an accessor is applied against an other value type, the value will be cast to a dictionary object. Attempting to cast null will throw, all other types cast to an empty dictionary.

#### Assumptions and limitations
* A parse error will occur if an accessor or accessor chain follows anything other than a pre-defined dictionary.
* No functions will create dictionaries.
* Custom dictionaries are not supported. Currently there is no use-case w.r.t. conditionals.

### Null
Null is a special type that is returned from dictionary accessor misses. There is no keyword for null.

TODO: Probably easy to add null keyword if required, but is there a use case?

## Type Casting

Based on the context, a value may be implicitly cast to another type.

### Boolean to Number
* False =\> 0
* True =\> 1

### Boolean to Version
* Not convertible

### Boolean to String
* False =\> 'False'
* True =\> 'True'

### Boolean to Dictionary
* Empty dictionary

### Number to Boolean
* 0 =\> False
* Otherwise True

### Number to Version
* Must be greater than zero and must contain a non-zero decimal. Must be less than Int32.MaxValue (decimal component also).

### Number to String
* Invariant-culture ToString

### Number to Dictionary
* Empty dictionary

### String to Boolean
* Empty string =\> False
* Otherwise True

### String to Number
* Parsed using invariant-culture and the following rules: AllowDecimalPoint | AllowLeadingSign | AllowLeadingWhite | AllowThousands | AllowTrailingWhite

### String to Version
* Must contain Major and Minor component at minimum.

### String to Dictionary
* Empty dictionary

### Version to Boolean
* True

### Version to Number
* Not convertible

### Version to String
* Major.Minor
* or Major.Minor.Build
* or Major.Minor.Build.Revision

### Version to Dictionary
* Empty dictionary

### Dictionary to Boolean
* True

### Dictionary to Number
* Not convertible

### Dictionary to Version
* Not convertible

### Dictionary to String
* Empty string

### Null to Boolean
* False

### Null to Number
* 0

### Null to Version
* Not convertible

### Null to String
* Empty string

### Null to Dictionary
* Not convertible

## Functions

### And
* Evaluates true if all parameters are true
* Min parameters: 2. Max parameters: N
* Converts parameters to Boolean for evaluation
* Short-circuits after first False

### Contains
* Evaluates true if left parameter string contains right parameter
* Min parameters: 2. Max parameters: 2
* Converts parameters to string for evaluation
* Performs ordinal ignore-case comparison

### EndsWith
* Evaluates true if left parameter string ends with right parameter
* Min parameters: 2. Max parameters: 2
* Converts parameters to string for evaluation
* Performs ordinal ignore-case comparison

### Eq
* Evaluates true if parameters are equal
* Min parameters: 2. Max parameters: 2
* Converts right parameter to match type of left parameter. Returns False if conversion fails.
* Ordinal ignore-case comparison for strings

### Ge
* Evaluates true if left parameter is greater than or equal to the right parameter
* Exactly 2 parameters
* Converts right parameter to match type of left parameter. Errors if conversion fails.
* Ordinal ignore-case comparison for strings

### Gt
* Evaluates true if left parameter is greater than the right parameter
* Min parameters: 2. Max parameters: 2
* Converts right parameter to match type of left parameter. Errors if conversion fails.
* Ordinal ignore-case comparison for strings

### In
* Evaluates true if left parameter is equal to any right parameter
* Min parameters: 1. Max parameters: N
* Converts right parameters to match type of left parameter. Equality comparison evaluates false if conversion fails.
* Ordinal ignore-case comparison for strings

### Le
* Evaluates true if left parameter is less than or equal to the right parameter
* Min parameters: 2. Max parameters: 2
* Converts right parameter to match type of left parameter. Errors if conversion fails.
* Ordinal ignore-case comparison for strings

### Lt
* Evaluates true if left parameter is less than the right parameter
* Min parameters: 2. Max parameters: 2
* Converts right parameter to match type of left parameter. Errors if conversion fails.
* Ordinal ignore-case comparison for strings

### Ne
* Evaluates true if parameters are not equal
* Min parameters: 2. Max parameters: 2
* Converts right parameter to match type of left parameter. Returns True if conversion fails.
* Ordinal ignore-case comparison for strings

### Not
* Evaluates true if parameter is false
* Min parameters: 1. Max parameters: 1
* Converts value to Boolean for evaluation

### NotIn
* Evaluates true if left parameter is not equal to any right parameter
* Min parameters: 1. Max parameters: N
* Converts right parameters to match type of left parameter. Equality comparison evaluates false if conversion fails.
* Ordinal ignore-case comparison for strings

### Or
* Evaluates true if any parameter is true
* Min parameters: 2. Max parameters: N
* Casts parameters to Boolean for evaluation
* Short-circuits after first True

### StartsWith
* Evaluates true if left parameter string starts with right parameter
* Min parameters: 2. Max parameters: 2
* Converts parameters to string for evaluation
* Performs ordinal ignore-case comparison

### Xor
* Evaluates true if exactly one parameter is true
* Min parameters: 2. Max parameters: 2
* Casts parameters to Boolean for evaluation
