using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SimpleContainer.Helpers.ReflectionEmit
{
	internal class SimpleExpressionEvaluator
	{
		private readonly ConcurrentDictionary<MethodBase, Func<object, object[], object>> methods =
			new ConcurrentDictionary<MethodBase, Func<object, object[], object>>();

		private static readonly MethodInfo objectEqualsMethod = typeof (object).GetMethod("Equals",
			new[] {typeof (object), typeof (object)});

		public object Evaluate(Expression expression)
		{
			if (expression == null)
				return null;
			var xLambda = expression as LambdaExpression;
			if (xLambda != null)
			{
				if (xLambda.Parameters.Count > 0)
					throw new InvalidOperationException("lambda expression must not have parameters");
				return Evaluate(xLambda.Body);
			}
			var xConstant = expression as ConstantExpression;
			if (xConstant != null)
				return xConstant.Value;
			var xUnary = expression as UnaryExpression;
			if (xUnary != null && xUnary.NodeType == ExpressionType.Convert)
				return Evaluate(xUnary.Operand);
			var xMember = expression as MemberExpression;
			if (xMember != null)
			{
				var obj = Evaluate(xMember.Expression);
				return UntypedMemberAccessor.Create(xMember.Member).Get(obj);
			}
			var xCall = expression as MethodCallExpression;
			if (xCall != null)
				return Invoke(xCall.Method, Evaluate(xCall.Object), xCall.Arguments);
			var xBinary = expression as BinaryExpression;
			if (xBinary != null)
			{
				var leftObj = Evaluate(xBinary.Left);
				var rightObj = Evaluate(xBinary.Right);
				if (xBinary.NodeType == ExpressionType.ArrayIndex)
					return ((Array) leftObj).GetValue((int) rightObj);
				var operatorMethod = xBinary.NodeType == ExpressionType.Equal ? objectEqualsMethod : xBinary.Method;
				if (operatorMethod == null)
					throw new InvalidOperationException("can't evaluate operator " + xBinary.NodeType);
				var method = methods.GetOrAdd(operatorMethod, ReflectionHelpers.EmitCallOf);
				return method.Invoke(null, new[] {leftObj, rightObj});
			}
			var xNew = expression as NewExpression;
			if (xNew != null)
				return Invoke(xNew.Constructor, null, xNew.Arguments);
			var xMemberInit = expression as MemberInitExpression;
			if (xMemberInit != null)
			{
				var instance = Evaluate(xMemberInit.NewExpression);
				foreach (var xBinding in xMemberInit.Bindings)
				{
					if (xBinding.BindingType != MemberBindingType.Assignment)
						throw new InvalidOperationException(string.Format("expression [{0}], member init type [{1}] is not supported",
							xBinding, xBinding.BindingType));
					var xAssignment = (MemberAssignment) xBinding;
					Invoke(((PropertyInfo) xAssignment.Member).GetSetMethod(), instance,
						EnumerableHelpers.Return(xAssignment.Expression));
				}
				return instance;
			}
			throw new InvalidOperationException("can't evaluate expression " + expression);
		}

		private object Invoke(MethodBase targetMethod, object target, IEnumerable<Expression> xArguments)
		{
			var arguments = xArguments.Select(Evaluate).ToArray();
			var method = methods.GetOrAdd(targetMethod, ReflectionHelpers.EmitCallOf);
			return method.Invoke(target, arguments);
		}
	}
}