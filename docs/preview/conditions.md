
# Task Conditions

## Types

### Boolean
`true` or `false` (ordinal case insensitive)

### Number
Starts with `-` `.` or `0-9`. Internally parses into .Net `Decimal` type using invariant culture.

Cannot contain `,` since it is a separator.

### String
Single-quoted, e.g. 'this is a string' or ''

Literal single-quote escaped by two single quotes, e.g. 'hey y''all'

### Version
Starts with a number and contains two or three `.`. Internall parses into .Net `Version` type.

Note, only one `.` is present, then the value would be parsed as a number.

## Type Casting

Based on the context, a value may be implicitly cast to another type.

### Boolean to Number
* False =\> 0
* True =\> 1

### Boolean to String
* False =\> 'False'
* True =\> 'True'

### Boolean to Version
* Not convertible

### Number to Boolean
* 0 =\> False
* Otherwise True

### Number to String
* Invariant culture to string

### Number to Version
* Must be greater than zero and must contain a non-zero decimal. Must be less than Int32.MaxValue (decimal component also).

### String to Boolean
* Empty string =\> False
* Otherwise True

### String to Number
* Parsed using invariant culture and the following rules: AllowDecimalPoint | AllowLeadingSign | AllowLeadingWhite | AllowThousands | AllowTrailingWhite

### String to Version
* Must contain Major and Minor component at minimum.

### Version to Boolean
* Always True

### Version to Number
* Not convertible

### Version to String
* Major.Minor
* or Major.Minor.Build
* or Major.Minor.Build.Revision

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
