using System.Runtime.Serialization;

namespace ParserCombinatorLib {

	public class ParsingException : Exception {
		public ParsingException() {
		}

		public ParsingException(string? message) : base(message) {
		}

		public ParsingException(string? message, Exception? innerException) : base(message, innerException) {
		}
	}

	public class Parser {
		public record ParserInput {
			public string String { get; init; }
			public int Index { get; init; }
		}

		public readonly struct ParserResult {
			public bool Success { get; init; }
			public dynamic? Value { get; init; }
			public int Index { get; init; }
		}

		public required string Name { get; init; }
		public required Func<ParserInput, ParserResult> Parse { get; init; }

		public Parser Map(Func<dynamic?, dynamic?> mapper) {
			return new Parser() {
				Name = $"{Name}_mapped",
				Parse = (input) => {
					var intermediate_result = Parse(input);

					if (!intermediate_result.Success) {
						return new ParserResult() {
							Success = false,
							Value = null,
							Index = input.Index,
						};
					}

					return new ParserResult() {
						Success = true,
						Value = mapper(intermediate_result.Value),
						Index = intermediate_result.Index,
					};
				}
			};
		}

		public Parser Expect(string? message = null) {
			return new Parser() {
				Name = $"{Name}_mapped",
				Parse = (input) => {
					var result = Parse(input);

					if (!result.Success) {
						throw new ParsingException($"{message ?? "ERROR"}\nat {input.Index}: {input.String[input.Index..]}");
					}

					return result;
				}
			};
		}

		public static Parser Char(char c, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					if (input.Index >= input.String.Length) throw new EndOfStreamException();

					var str_slice = input.String[input.Index..];
					var first_char = str_slice[0];

					if (first_char != c) {
						return new ParserResult() {
							Success = false,
							Value = null,
							Index = input.Index,
						};
					}

					return new ParserResult() {
						Success = true,
						Value = first_char,
						Index = input.Index + 1,
					};
				}
			};
		}

		public static Parser Satisfies(Func<char, bool> predicate, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					if (input.Index >= input.String.Length) throw new EndOfStreamException();

					var str_slice = input.String[input.Index..];
					var first_char = str_slice[0];

					if (!predicate(first_char)) {
						return new ParserResult() {
							Success = false,
							Value = null,
							Index = input.Index,
						};
					}

					return new ParserResult() {
						Success = true,
						Value = first_char,
						Index = input.Index + 1,
					};
				}
			};
		}

		public static Parser Any_of(IEnumerable<char> chars, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					if (input.Index >= input.String.Length) throw new EndOfStreamException();

					var str_slice = input.String[input.Index..];
					var first_char = str_slice[0];

					if (!chars.Contains(first_char)) {
						return new ParserResult() {
							Success = false,
							Value = null,
							Index = input.Index,
						};
					}

					return new ParserResult() {
						Success = true,
						Value = first_char,
						Index = input.Index + 1,
					};
				}
			};
		}

		public static Parser Any(IEnumerable<Parser> parsers, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					foreach (var parser in parsers) {
						var result = parser.Parse(input);

						if (result.Success) return result;
					}

					return new ParserResult() {
						Success = false,
						Value = null,
						Index = input.Index,
					};
				}
			};
		}

		public static Parser Many(Parser parser, string name, int? min = null, uint? max = null) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					List<dynamic?> list = [];

					var intermediate_input = input;

					while (true) {
						if (input.Index >= input.String.Length) {
							return new ParserResult() {
								Success = true,
								Value = list,
								Index = intermediate_input.Index,
							};
						}

						var result = parser.Parse(intermediate_input);
						if (!result.Success) {
							break;
						}

						intermediate_input = intermediate_input with { Index = result.Index };

						list.Add(result.Value);

						if (list.Count == max) break;
					}

					if (min is not null && list.Count < min.Value) {
						return new ParserResult() {
							Success = false,
							Value = null,
							Index = input.Index,
						};
					}

					return new ParserResult() {
						Success = true,
						Value = list,
						Index = intermediate_input.Index,
					};
				}
			};
		}

		public class SequenceElement {
			public Parser Parser => ParserDirect ?? ParserFunc();

			public Parser? ParserDirect { get; init; }
			public Func<Parser>? ParserFunc { get; init; }

			public bool Optional { get; set; }
			public bool Include { get; set; }
		}

		public static Parser Sequence(IEnumerable<SequenceElement> sequence_elements, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					List<dynamic?> list = [];

					var intermediate_input = input;

					foreach (var sequence_element in sequence_elements) {
						try {
							var result = sequence_element.Parser.Parse(intermediate_input);

							if (!result.Success) {
								if (sequence_element.Optional) continue;

								return new ParserResult() {
									Success = false,
									Value = list,
									Index = intermediate_input.Index,
								};
							}

							intermediate_input = intermediate_input with { Index = result.Index };

							if (sequence_element.Include) {
								list.Add(result.Value);
							}
						} catch (ParsingException) {
							if (sequence_element.Optional) continue;

							throw;
						}
					}

					return new ParserResult() {
						Success = true,
						Value = list,
						Index = intermediate_input.Index,
					};
				}
			};
		}


		public static Parser Literal(string literal, dynamic? value, string name) {
			return new Parser() {
				Name = name,
				Parse = (input) => {
					var x = literal.Select(c => {
						return new SequenceElement() { ParserDirect = Char(c, $"{name}_{c}"), Optional = false, Include = false };
					});

					var result = Sequence(x, $"{name}_sequence").Parse(input);

					if (!result.Success) {
						return new ParserResult() {
							Success = false,
							Value = null,
							Index = input.Index,
						};
					}

					return new ParserResult() {
						Success = true,
						Value = value,
						Index = result.Index,
					};
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
