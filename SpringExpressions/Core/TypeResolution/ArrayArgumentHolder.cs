using System.Linq;
using JetBrains.Annotations;

using SpringUtil;

namespace SpringCore.TypeResolution
{
    public class ArrayArgumentHolder
    {
        public static bool TryCreateArrayArgumentHolder(
            [NotNull] string originalString, 
            out ArrayArgumentHolder argumentHolder)
        {
            // todo: wiele tabel... wiele tablic... pytanie, co kiedy kto urywa...


            string arrayDeclaration;
            string arrayItemTypeName; 

            var indexOf = originalString.IndexOf('[');

            if (originalString.Length > 0 && indexOf > -1)
            {
                arrayItemTypeName = originalString.Substring(0, indexOf);
                var remainder = originalString.Substring(indexOf);

                string[] remainderParts = StringUtils.Split(
                    s: remainder, 
                    delimiters: ",", 
                    trimTokens: false, 
                    ignoreEmptyTokens: false, 
                    quoteChars: "[]");

                string arrayPart = remainderParts[0].Trim();

                // todo: wtf!!!:
                if (arrayPart[0] == '[' && arrayPart[arrayPart.Length - 1] == ']')
                {
                    arrayDeclaration = arrayPart;

                    var arrayPartsJoinedWithoutElement0 = string.Join(
                        separator: ",",
                        value: remainderParts,
                        startIndex: 1,
                        count: remainderParts.Length - 1);

                    if (!string.IsNullOrEmpty(arrayPartsJoinedWithoutElement0))
                        remainder = ", " + arrayPartsJoinedWithoutElement0;
                    else
                        remainder = "";


                    argumentHolder = new ArrayArgumentHolder(
                        arrayItemTypeName + remainder, 
                        arrayDeclaration);

                    return true;
                    //originalString = ", " + string.Join(",", remainderParts, 1, remainderParts.Length - 1);
                }

                argumentHolder = null;
                return false;
                //unresolvedGenericMethodName = name + remainder;
                //unresolvedGenericTypeName = name + "`" + unresolvedGenericArguments.Length + remainder;

            }

            argumentHolder = null;
            return false;
        }


        private ArrayArgumentHolder(
            [NotNull] string arrayItemTypeName, 
            [NotNull] string arrayDeclaration)
        {
            ArrayItemTypeName = arrayItemTypeName;
            ArrayDeclaration = arrayDeclaration;

            //[,][] => [][,]
            var reversed = arrayDeclaration.Reverse().ToArray();
            for (var i = 0; i < reversed.Length; ++i)
                switch (reversed[i])
                {
                    case '[':
                        reversed[i] = ']';
                        break;
                    case ']':
                        reversed[i] = '[';
                        break;
                }

            ArrayDeclarationReversed = new string(reversed);
        }

        public string ArrayItemTypeName { get; }
        public string ArrayDeclaration { get; }
        public string ArrayDeclarationReversed { get; }
    }
}
