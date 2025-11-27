using System;
using System.Text;

namespace Vault;

public static class Encoder {
  private const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
  private const int BaseValue = 36;

  public static string Encode(long idNumber, int length = 8) {
    if (idNumber < 0) {
      throw new ArgumentException("ID must be non-negative");
    }

    if (idNumber == 0) {
      return new string('0', length);
    }

    var result = new StringBuilder();
    long value = idNumber;

    while (value > 0) {
      long rem = value % BaseValue;
      value /= BaseValue;
      result.Insert(0, Chars[(int)rem]);
    }

    while (result.Length < length) {
      result.Insert(0, '0');
    }

    return result.ToString();
  }

  public static long Decode(string code) {
    return Convert.ToInt64(code.ToUpperInvariant(), BaseValue);
  }
}
