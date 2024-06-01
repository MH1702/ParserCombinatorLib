namespace ParserCombinatorLib {
	public class Parser {
		public required string Name { get; init; }
		public required Func<(string, int), (bool, dynamic?, int)> Parse { get; init; }

		public Parser Map(Func<dynamic?, dynamic?> mapper) {
			return new Parser() {
				Name = $"{Name}_mapped",
				Parse = (input) => {
					var x = Parse(input);

					if (!x.Item1) {
						return (false, null, input.Item2);
					}

					return (true, mapper(x.Item2), x.Item3);
				}
			};
		}

		public Parser Expect(string? message = null) {
			return new Parser() {
				Name = $"{Name}_mapped",
				Parse = (input) => {
					var x = Parse(input);

					if (!x.Item1) {
						throw new Exception($"{message ?? "ERROR"}\nat {input.Item2}: {input.Item1[input.Item2..]}");
					}

					return x;
				}
			};
		}

		public static Parser Char(char c, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					if (input.Item2 >= input.Item1.Length) throw new EndOfStreamException();

					var (str, i) = input;

					var str_slice = str[i..];
					var first_char = str_slice[0];

					if (first_char == c) return (true, first_char, i + 1);
					else return (false, null, i);
				}
			};
		}

		public static Parser Satisfies(Func<char, bool> predicate, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					if (input.Item2 >= input.Item1.Length) throw new EndOfStreamException();

					var (str, i) = input;

					var str_slice = str[i..];
					var first_char = str_slice[0];

					if (predicate(first_char)) return (true, first_char, i + 1);
					else return (false, null, i);
				}
			};
		}

		public static Parser Any_of(IEnumerable<char> chars, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					if (input.Item2 >= input.Item1.Length) throw new EndOfStreamException();

					var (str, i) = input;

					var str_slice = str[i..];
					var first_char = str_slice[0];

					if (chars.Contains(first_char)) return (true, first_char, i + 1);
					else return (false, null, i);
				}
			};
		}

		public static Parser Any(IEnumerable<Parser> parsers, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					foreach (var parser in parsers) {
						var x = parser.Parse(input);

						if (x.Item1) return x;
					}

					return (false, null, input.Item2);
				}
			};
		}

		public static Parser Many(Parser parser, string name, int? min = null, uint? max = null) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					List<dynamic?> list = [];

					var x = input;

					while (true) {
						if (input.Item2 >= input.Item1.Length) return (true, list, x.Item2);

						var (success, result, index) = parser.Parse(x);
						if (!success) {
							break;
						}

						x = (x.Item1, index);

						list.Add(result);

						if (list.Count == max) break;
					}

					if (min is not null && list.Count < min.Value) {
						return (false, null, input.Item2);
					}

					return (true, list, x.Item2);
				}
			};
		}

		public class SequenceElement {
			public Parser ActualParser => Parser ?? ParserFunc();

			public Parser? Parser { get; init; }
			public Func<Parser>? ParserFunc { get; init; }

			public bool Optional { get; set; }
			public bool Include { get; set; }
		}

		public static Parser Sequence(IEnumerable<SequenceElement> parsers, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					List<dynamic?> list = [];

					var x = input;

					foreach (var y in parsers) {
						var (success, result, index) = y.ActualParser.Parse(x);

						if (!success) {
							if (y.Optional) continue;

							return (false, list, x.Item2);
						}

						x = (x.Item1, index);

						if (y.Include) {
							list.Add(result);
						}
					}

					return (true, list, x.Item2);
				}
			};
		}


		public static Parser Literal(string literal, dynamic? value, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					var x = literal.Select(c => {
						return new SequenceElement() { Parser = Char(c, $"{name}_{c}"), Optional = false, Include = false };
					});

					var (success, _, remaining) = Sequence(x, $"{name}_sequence").Parse(input);

					if (!success) return (false, null, input.Item2);
					else return (true, value, remaining);
				}
			};
		}


		public static Parser Letter() {
			return Satisfies(char.IsLetter, "Any Letter");
		}

		public static Parser WhitespaceChar() {
			return Satisfies(char.IsWhiteSpace, "Any Whitespace Char");
		}

		public static Parser Whitespace() {
			return Many(WhitespaceChar(), "Any Whitespace");
		}

		public static Parser Digit() {
			return Satisfies(char.IsDigit, "Any Digit");
		}
	}
}
