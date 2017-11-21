﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace SqlNado.Utilities
{
    public abstract class ExpressionVisitor
    {
        protected ExpressionVisitor()
        {
        }

        protected virtual Expression Visit(Expression exp)
        {
            if (exp == null)
                return null;

            switch (exp.NodeType)
            {
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.Not:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.ArrayLength:
                case ExpressionType.Quote:
                case ExpressionType.TypeAs:
                case ExpressionType.UnaryPlus:
                    return VisitUnary((UnaryExpression)exp);

                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.Coalesce:
                case ExpressionType.ArrayIndex:
                case ExpressionType.RightShift:
                case ExpressionType.LeftShift:
                case ExpressionType.ExclusiveOr:
                case ExpressionType.Power:
                    return VisitBinary((BinaryExpression)exp);

                case ExpressionType.TypeIs:
                    return VisitTypeIs((TypeBinaryExpression)exp);

                case ExpressionType.Conditional:
                    return VisitConditional((ConditionalExpression)exp);

                case ExpressionType.Constant:
                    return VisitConstant((ConstantExpression)exp);

                case ExpressionType.Parameter:
                    return VisitParameter((ParameterExpression)exp);

                case ExpressionType.MemberAccess:
                    return VisitMemberAccess((MemberExpression)exp);

                case ExpressionType.Call:
                    return VisitMethodCall((MethodCallExpression)exp);

                case ExpressionType.Lambda:
                    return VisitLambda((LambdaExpression)exp);

                case ExpressionType.New:
                    return VisitNew((NewExpression)exp);

                case ExpressionType.NewArrayInit:
                case ExpressionType.NewArrayBounds:
                    return VisitNewArray((NewArrayExpression)exp);

                case ExpressionType.Invoke:
                    return VisitInvocation((InvocationExpression)exp);

                case ExpressionType.MemberInit:
                    return VisitMemberInit((MemberInitExpression)exp);

                case ExpressionType.ListInit:
                    return VisitListInit((ListInitExpression)exp);

                default:
                    return VisitUnknown(exp);
            }
        }

        protected virtual Expression VisitUnknown(Expression expression) => throw new Exception(string.Format("Unhandled expression type: '{0}'", expression.NodeType));

        protected virtual MemberBinding VisitBinding(MemberBinding binding)
        {
            switch (binding.BindingType)
            {
                case MemberBindingType.Assignment:
                    return VisitMemberAssignment((MemberAssignment)binding);

                case MemberBindingType.MemberBinding:
                    return VisitMemberMemberBinding((MemberMemberBinding)binding);

                case MemberBindingType.ListBinding:
                    return VisitMemberListBinding((MemberListBinding)binding);

                default:
                    throw new Exception(string.Format("Unhandled binding type '{0}'", binding.BindingType));
            }
        }

        protected virtual ElementInit VisitElementInitializer(ElementInit initializer)
        {
            var arguments = VisitExpressionList(initializer.Arguments);
            if (arguments != initializer.Arguments)
                return Expression.ElementInit(initializer.AddMethod, arguments);

            return initializer;
        }

        protected virtual Expression VisitUnary(UnaryExpression u)
        {
            var operand = Visit(u.Operand);
            return UpdateUnary(u, operand, u.Type, u.Method);
        }

        protected UnaryExpression UpdateUnary(UnaryExpression u, Expression operand, Type resultType, MethodInfo method)
        {
            if (u.Operand != operand || u.Type != resultType || u.Method != method)
            {
                return Expression.MakeUnary(u.NodeType, operand, resultType, method);
            }
            return u;
        }

        protected virtual Expression VisitBinary(BinaryExpression b)
        {
            var left = Visit(b.Left);
            var right = Visit(b.Right);
            var conversion = Visit(b.Conversion);
            return UpdateBinary(b, left, right, conversion, b.IsLiftedToNull, b.Method);
        }

        protected BinaryExpression UpdateBinary(BinaryExpression b, Expression left, Expression right, Expression conversion, bool isLiftedToNull, MethodInfo method)
        {
            if (left != b.Left || right != b.Right || conversion != b.Conversion || method != b.Method || isLiftedToNull != b.IsLiftedToNull)
            {
                if (b.NodeType == ExpressionType.Coalesce && b.Conversion != null)
                    return Expression.Coalesce(left, right, conversion as LambdaExpression);

                return Expression.MakeBinary(b.NodeType, left, right, isLiftedToNull, method);
            }
            return b;
        }

        protected virtual Expression VisitTypeIs(TypeBinaryExpression b)
        {
            var expr = Visit(b.Expression);
            return UpdateTypeIs(b, expr, b.TypeOperand);
        }

        protected TypeBinaryExpression UpdateTypeIs(TypeBinaryExpression binaryExpression, Expression expression, Type typeOperand)
        {
            if (expression != binaryExpression.Expression || typeOperand != binaryExpression.TypeOperand)
                return Expression.TypeIs(expression, typeOperand);

            return binaryExpression;
        }

        protected virtual Expression VisitConstant(ConstantExpression constantExpression) => constantExpression;

        protected virtual Expression VisitConditional(ConditionalExpression conditionalExpression)
        {
            var test = Visit(conditionalExpression.Test);
            var ifTrue = Visit(conditionalExpression.IfTrue);
            var ifFalse = Visit(conditionalExpression.IfFalse);
            return UpdateConditional(conditionalExpression, test, ifTrue, ifFalse);
        }

        protected ConditionalExpression UpdateConditional(ConditionalExpression conditionalExpression, Expression test, Expression ifTrue, Expression ifFalse)
        {
            if (test != conditionalExpression.Test || ifTrue != conditionalExpression.IfTrue || ifFalse != conditionalExpression.IfFalse)
                return Expression.Condition(test, ifTrue, ifFalse);

            return conditionalExpression;
        }

        protected virtual Expression VisitParameter(ParameterExpression parameterExpression) => parameterExpression;

        protected virtual Expression VisitMemberAccess(MemberExpression memberExpression)
        {
            var exp = Visit(memberExpression.Expression);
            return UpdateMemberAccess(memberExpression, exp, memberExpression.Member);
        }

        protected MemberExpression UpdateMemberAccess(MemberExpression memberExpression, Expression expression, MemberInfo member)
        {
            if (expression != memberExpression.Expression || member != memberExpression.Member)
                return Expression.MakeMemberAccess(expression, member);

            return memberExpression;
        }

        protected virtual Expression VisitMethodCall(MethodCallExpression callExpression)
        {
            var obj = Visit(callExpression.Object);
            var args = VisitExpressionList(callExpression.Arguments);
            return UpdateMethodCall(callExpression, obj, callExpression.Method, args);
        }

        protected MethodCallExpression UpdateMethodCall(MethodCallExpression callExpression, Expression obj, MethodInfo method, IEnumerable<Expression> args)
        {
            if (obj != callExpression.Object || method != callExpression.Method || args != callExpression.Arguments)
                return Expression.Call(obj, method, args);

            return callExpression;
        }

        protected virtual ReadOnlyCollection<Expression> VisitExpressionList(ReadOnlyCollection<Expression> original)
        {
            if (original != null)
            {
                List<Expression> list = null;
                for (int i = 0, n = original.Count; i < n; i++)
                {
                    Expression p = Visit(original[i]);
                    if (list != null)
                    {
                        list.Add(p);
                    }
                    else if (p != original[i])
                    {
                        list = new List<Expression>(n);
                        for (int j = 0; j < i; j++)
                        {
                            list.Add(original[j]);
                        }
                        list.Add(p);
                    }
                }

                if (list != null)
                    return list.AsReadOnly();
            }
            return original;
        }

        protected virtual ReadOnlyCollection<Expression> VisitMemberAndExpressionList(ReadOnlyCollection<MemberInfo> members, ReadOnlyCollection<Expression> original)
        {
            if (original != null)
            {
                List<Expression> list = null;
                for (int i = 0, n = original.Count; i < n; i++)
                {
                    Expression p = VisitMemberAndExpression(members?[i], original[i]);
                    if (list != null)
                    {
                        list.Add(p);
                    }
                    else if (p != original[i])
                    {
                        list = new List<Expression>(n);
                        for (int j = 0; j < i; j++)
                        {
                            list.Add(original[j]);
                        }
                        list.Add(p);
                    }
                }

                if (list != null)
                    return list.AsReadOnly();
            }
            return original;
        }

        protected virtual Expression VisitMemberAndExpression(MemberInfo member, Expression expression) => Visit(expression);

        protected virtual MemberAssignment VisitMemberAssignment(MemberAssignment assignment)
        {
            var expression = Visit(assignment.Expression);
            return UpdateMemberAssignment(assignment, assignment.Member, expression);
        }

        protected MemberAssignment UpdateMemberAssignment(MemberAssignment assignment, MemberInfo member, Expression expression)
        {
            if (expression != assignment.Expression || member != assignment.Member)
                return Expression.Bind(member, expression);

            return assignment;
        }

        protected virtual MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding binding)
        {
            var bindings = VisitBindingList(binding.Bindings);
            return UpdateMemberMemberBinding(binding, binding.Member, bindings);
        }

        protected MemberMemberBinding UpdateMemberMemberBinding(MemberMemberBinding binding, MemberInfo member, IEnumerable<MemberBinding> bindings)
        {
            if (bindings != binding.Bindings || member != binding.Member)
                return Expression.MemberBind(member, bindings);

            return binding;
        }

        protected virtual MemberListBinding VisitMemberListBinding(MemberListBinding binding)
        {
            var initializers = VisitElementInitializerList(binding.Initializers);
            return UpdateMemberListBinding(binding, binding.Member, initializers);
        }

        protected MemberListBinding UpdateMemberListBinding(MemberListBinding binding, MemberInfo member, IEnumerable<ElementInit> initializers)
        {
            if (initializers != binding.Initializers || member != binding.Member)
                return Expression.ListBind(member, initializers);

            return binding;
        }

        protected virtual IEnumerable<MemberBinding> VisitBindingList(ReadOnlyCollection<MemberBinding> original)
        {
            List<MemberBinding> list = null;
            for (int i = 0, n = original.Count; i < n; i++)
            {
                MemberBinding b = VisitBinding(original[i]);
                if (list != null)
                {
                    list.Add(b);
                }
                else if (b != original[i])
                {
                    list = new List<MemberBinding>(n);
                    for (int j = 0; j < i; j++)
                    {
                        list.Add(original[j]);
                    }
                    list.Add(b);
                }
            }

            if (list != null)
                return list;

            return original;
        }

        protected virtual IEnumerable<ElementInit> VisitElementInitializerList(ReadOnlyCollection<ElementInit> original)
        {
            List<ElementInit> list = null;
            for (int i = 0, n = original.Count; i < n; i++)
            {
                ElementInit init = VisitElementInitializer(original[i]);
                if (list != null)
                {
                    list.Add(init);
                }
                else if (init != original[i])
                {
                    list = new List<ElementInit>(n);
                    for (int j = 0; j < i; j++)
                    {
                        list.Add(original[j]);
                    }
                    list.Add(init);
                }
            }

            if (list != null)
                return list;

            return original;
        }

        protected virtual Expression VisitLambda(LambdaExpression lambda)
        {
            var body = Visit(lambda.Body);
            return UpdateLambda(lambda, lambda.Type, body, lambda.Parameters);
        }

        protected LambdaExpression UpdateLambda(LambdaExpression lambda, Type delegateType, Expression body, IEnumerable<ParameterExpression> parameters)
        {
            if (body != lambda.Body || parameters != lambda.Parameters || delegateType != lambda.Type)
                return Expression.Lambda(delegateType, body, parameters);

            return lambda;
        }

        protected virtual NewExpression VisitNew(NewExpression nex)
        {
            var args = VisitMemberAndExpressionList(nex.Members, nex.Arguments);
            return UpdateNew(nex, nex.Constructor, args, nex.Members);
        }

        protected NewExpression UpdateNew(NewExpression nex, ConstructorInfo constructor, IEnumerable<Expression> args, IEnumerable<MemberInfo> members)
        {
            if (args != nex.Arguments || constructor != nex.Constructor || members != nex.Members)
            {
                if (nex.Members != null)
                    return Expression.New(constructor, args, members);

                return Expression.New(constructor, args);
            }
            return nex;
        }

        protected virtual Expression VisitMemberInit(MemberInitExpression init)
        {
            var newExpression = VisitNew(init.NewExpression);
            var bindings = VisitBindingList(init.Bindings);
            return UpdateMemberInit(init, newExpression, bindings);
        }

        protected MemberInitExpression UpdateMemberInit(MemberInitExpression init, NewExpression newExpression, IEnumerable<MemberBinding> bindings)
        {
            if (newExpression != init.NewExpression || bindings != init.Bindings)
                return Expression.MemberInit(newExpression, bindings);

            return init;
        }

        protected virtual Expression VisitListInit(ListInitExpression init)
        {
            var newExpression = VisitNew(init.NewExpression);
            var initializers = VisitElementInitializerList(init.Initializers);
            return UpdateListInit(init, newExpression, initializers);
        }

        protected ListInitExpression UpdateListInit(ListInitExpression init, NewExpression newExpression, IEnumerable<ElementInit> initializers)
        {
            if (newExpression != init.NewExpression || initializers != init.Initializers)
                return Expression.ListInit(newExpression, initializers);

            return init;
        }

        protected virtual Expression VisitNewArray(NewArrayExpression na)
        {
            IEnumerable<Expression> exprs = VisitExpressionList(na.Expressions);
            return UpdateNewArray(na, na.Type, exprs);
        }

        protected NewArrayExpression UpdateNewArray(NewArrayExpression newArrayExpression, Type arrayType, IEnumerable<Expression> expressions)
        {
            if (expressions != newArrayExpression.Expressions || newArrayExpression.Type != arrayType)
            {
                if (newArrayExpression.NodeType == ExpressionType.NewArrayInit)
                    return Expression.NewArrayInit(arrayType.GetElementType(), expressions);

                return Expression.NewArrayBounds(arrayType.GetElementType(), expressions);
            }
            return newArrayExpression;
        }

        protected virtual Expression VisitInvocation(InvocationExpression iv)
        {
            var args = VisitExpressionList(iv.Arguments);
            var expr = Visit(iv.Expression);
            return UpdateInvocation(iv, expr, args);
        }

        protected InvocationExpression UpdateInvocation(InvocationExpression invocationExpression, Expression expression, IEnumerable<Expression> args)
        {
            if (args != invocationExpression.Arguments || expression != invocationExpression.Expression)
                return Expression.Invoke(expression, args);

            return invocationExpression;
        }
    }
}
