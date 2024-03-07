using System;

namespace SpringExpressions.Expressions.Compiling.Expressions
{
       // todo: error: public? name?

    internal class CompileErrorException : Exception
    {
        public CompileErrorException(string message) : base(message)
        {
        }
    }
}
