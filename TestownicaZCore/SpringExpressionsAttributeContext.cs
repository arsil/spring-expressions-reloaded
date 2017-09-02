using System;
using System.Collections;
using System.Collections.Generic;
using Core.Types;

internal class SpringExpressionAttributesContext
{
	/// <summary>
	/// Konstruuje obiekt
	/// </summary>
	/// <param name="attributes">Mapa atrybutów</param>
	public SpringExpressionAttributesContext(
		[NotNull] IDictionary<string, Decimal2> attributes)
	{
		_attributes = attributes;
	}

	public decimal? this[string paramName]
	{ get { return Get(paramName); } }

	public decimal? Get([NotNull] string parameterName)
	{
		if (parameterName == null) throw new ArgumentNullException("parameterName");

		Decimal2 result;
		if (_attributes.TryGetValue(parameterName, out result))
			return result;

		return null;
	}

	public decimal this[string paramName, decimal defaultValue]
	{ get { return Nvl(paramName, defaultValue); } }

	public decimal GetNvl(string paramName, decimal defaultValue)
	{ return Nvl(paramName, defaultValue); }


	public decimal Nvl([NotNull] string attributeName, decimal defaultValue)
	{
		if (attributeName == null) throw new ArgumentNullException("attributeName");

		Decimal2 result;
		if (!_attributes.TryGetValue(attributeName, out result))
			result = (Decimal2)defaultValue;

		return result;
	}

	public bool Contains([NotNull] string attributeName)
	{
		if (attributeName == null) throw new ArgumentNullException("attributeName");

		return _attributes.ContainsKey(attributeName);
	}

	public bool Has([NotNull] string attributeName)
	{
		if (attributeName == null) throw new ArgumentNullException("attributeName");

		return _attributes.ContainsKey(attributeName);
	}

	public bool Exists([NotNull] string attributeName)
	{
		if (attributeName == null) throw new ArgumentNullException("attributeName");

		return _attributes.ContainsKey(attributeName);
	}


	public int GetMult(
		[NotNull] string attributeName, decimal threshold, decimal thresholdOffset)
	{
		Decimal2 attributeValue;
		_attributes.TryGetValue(attributeName, out attributeValue);

		// przy progu zerowym nie możemy dzielić, tylko sprawdzamy czy przekroczyliśmy
		// wartość offsetu
		if (threshold == 0)
		{
			return attributeValue >= thresholdOffset ? 1 : 0;
		}

		// skorygowanie wartości offsetem
		attributeValue -= (Decimal2)thresholdOffset;

		var multiplicity = (int)decimal.Floor(attributeValue / threshold);

		// jeśli attributeValue i Threshold mają przeciwne znaki,
		// to wyjdzie ujemna wartość - nie chcemy takowej, więc przycinamy
		// od dołu w zerze
		return Math.Max(0, multiplicity);
	}

	/// <summary>
	/// Wyciąga z podanej listy (tablicy atrybutów) atrybut o maksymalnej wartości
	/// i zwraca Tuple nazwa atrybutu na jego wartość.
	/// Gdy brak wartości atrybutów, to zwróci Decimal2.Min i pusty string.
	/// </summary>
	public Tuple<string, Decimal2> SelectMax(IEnumerable attributeNames)
	{
		Decimal2 resultVal = Decimal2.MinValue;
		string resultName = "";

		foreach (string str in attributeNames)
		{
			Decimal2 value;
			if (_attributes.TryGetValue(str, out value))
			{
				if (value >= resultVal)
				{
					resultVal = value;
					resultName = str;
				}
			}
		}

		return Tuple.Create(resultName, resultVal);
	}

	/// <summary>
	/// Wyciąga z podanej listy (tablicy atrybutów) atrybut o maksymalnej wartości
	/// i zwraca Tuple nazwa atrybutu na jego wartość.
	/// Gdy brak wartości atrybutów, to zwróci Decimal2.Min i pusty string.
	/// </summary>
	public Tuple<string, Decimal2> SelectMin(IEnumerable attributeNames)
	{
		Decimal2 resultVal = Decimal2.MaxValue;
		string resultName = "";

		foreach (string str in attributeNames)
		{
			Decimal2 value;
			if (_attributes.TryGetValue(str, out value))
			{
				if (value <= resultVal)
				{
					resultVal = value;
					resultName = str;
				}
			}
		}

		return Tuple.Create(resultName, resultVal);
	}

	/// <summary>
	/// Zlicza liczbę istniejących atrybutów
	/// </summary>
	public int CountExisting(IEnumerable attributeNames)
	{
		int result = 0;
		foreach (string attributeName in attributeNames)
			if (_attributes.ContainsKey(attributeName))
				++result;

		return result;
	}

	private readonly IDictionary<string, Decimal2> _attributes;
}
