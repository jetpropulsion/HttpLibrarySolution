using System;

namespace HttpLibrary
{
	/// <summary>
	/// Describes a configuration option with its metadata and constraints
	/// </summary>
	public sealed class ConfigOptionDefinition
	{
		/// <summary>
		/// Name of the configuration option (matches JSON property name, case-insensitive)
		/// </summary>
		public string Name { get; init; }

		/// <summary>
		/// Type of the configuration option
		/// </summary>
		public Type OptionType { get; init; }

		/// <summary>
		/// Whether this option is mandatory (must be present in JSON)
		/// </summary>
		public bool IsMandatory { get; init; }

		/// <summary>
		/// Minimum value constraint for numeric and date/time types (null if no constraint)
		/// </summary>
		public object? MinValue { get; init; }

		/// <summary>
		/// Maximum value constraint for numeric and date/time types (null if no constraint)
		/// </summary>
		public object? MaxValue { get; init; }

		/// <summary>
		/// Default value if not specified (for optional properties)
		/// </summary>
		public object? DefaultValue { get; init; }

		/// <summary>
		/// Additional description or validation notes
		/// </summary>
		public string? Description { get; init; }

		public ConfigOptionDefinition(string name, Type optionType, bool isMandatory)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			OptionType = optionType ?? throw new ArgumentNullException(nameof(optionType));
			IsMandatory = isMandatory;
		}

		/// <summary>
		/// Validates a value against this option's constraints
		/// </summary>
		/// <param name="value">Value to validate</param>
		/// <returns>True if valid, false otherwise</returns>
		public bool ValidateValue(object? value)
		{
			if(value is null)
			{
				return !IsMandatory;
			}

			// Type validation
			if(!OptionType.IsAssignableFrom(value.GetType()))
			{
				// Check for nullable types
				Type? underlyingType = Nullable.GetUnderlyingType(OptionType);
				if(underlyingType is not null && !underlyingType.IsAssignableFrom(value.GetType()))
				{
					return false;
				}
			}

			// Numeric and date/time range validation
			if(MinValue is not null || MaxValue is not null)
			{
				if(value is int intValue)
				{
					if(MinValue is int minInt && intValue < minInt)
					{
						return false;
					}
					if(MaxValue is int maxInt && intValue > maxInt)
					{
						return false;
					}
				}
				else if(value is long longValue)
				{
					if(MinValue is long minLong && longValue < minLong)
					{
						return false;
					}
					if(MaxValue is long maxLong && longValue > maxLong)
					{
						return false;
					}
				}
				else if(value is double doubleValue)
				{
					if(MinValue is double minDouble && doubleValue < minDouble)
					{
						return false;
					}
					if(MaxValue is double maxDouble && doubleValue > maxDouble)
					{
						return false;
					}
				}
				else if(value is float floatValue)
				{
					if(MinValue is float minFloat && floatValue < minFloat)
					{
						return false;
					}
					if(MaxValue is float maxFloat && floatValue > maxFloat)
					{
						return false;
					}
				}
				else if(value is decimal decimalValue)
				{
					if(MinValue is decimal minDecimal && decimalValue < minDecimal)
					{
						return false;
					}
					if(MaxValue is decimal maxDecimal && decimalValue > maxDecimal)
					{
						return false;
					}
				}
				else if(value is DateTime dateTimeValue)
				{
					if(MinValue is DateTime minDateTime && dateTimeValue < minDateTime)
					{
						return false;
					}
					if(MaxValue is DateTime maxDateTime && dateTimeValue > maxDateTime)
					{
						return false;
					}
				}
				else if(value is DateTimeOffset dateTimeOffsetValue)
				{
					if(MinValue is DateTimeOffset minDateTimeOffset && dateTimeOffsetValue < minDateTimeOffset)
					{
						return false;
					}
					if(MaxValue is DateTimeOffset maxDateTimeOffset && dateTimeOffsetValue > maxDateTimeOffset)
					{
						return false;
					}
				}
				else if(value is DateOnly dateOnlyValue)
				{
					if(MinValue is DateOnly minDateOnly && dateOnlyValue < minDateOnly)
					{
						return false;
					}
					if(MaxValue is DateOnly maxDateOnly && dateOnlyValue > maxDateOnly)
					{
						return false;
					}
				}
				else if(value is TimeOnly timeOnlyValue)
				{
					if(MinValue is TimeOnly minTimeOnly && timeOnlyValue < minTimeOnly)
					{
						return false;
					}
					if(MaxValue is TimeOnly maxTimeOnly && timeOnlyValue > maxTimeOnly)
					{
						return false;
					}
				}
				else if(value is TimeSpan timeSpanValue)
				{
					if(MinValue is TimeSpan minTimeSpan && timeSpanValue < minTimeSpan)
					{
						return false;
					}
					if(MaxValue is TimeSpan maxTimeSpan && timeSpanValue > maxTimeSpan)
					{
						return false;
					}
				}
			}

			return true;
		}
	}
}