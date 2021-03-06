﻿namespace Microsoft.AspNet.OData
{
    using Microsoft.OData.Edm;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    struct ClassProperty
    {
        internal readonly Type Type;
        internal readonly string Name;

        internal ClassProperty( PropertyInfo clrProperty, Type propertyType )
        {
            Contract.Requires( clrProperty != null );
            Contract.Requires( propertyType != null );

            Name = clrProperty.Name;
            Type = propertyType;
            Attributes = AttributesFromProperty( clrProperty );
        }

        internal ClassProperty( IServiceProvider services, IEnumerable<Assembly> assemblies, IEdmOperationParameter parameter, IModelTypeBuilder typeBuilder )
        {
            Contract.Requires( services != null );
            Contract.Requires( assemblies != null );
            Contract.Requires( parameter != null );
            Contract.Requires( typeBuilder != null );

            Name = parameter.Name;
            var context = new TypeSubstitutionContext( services, assemblies, typeBuilder );

            if ( parameter.Type.IsCollection() )
            {
                var collectionType = parameter.Type.AsCollection();
                var elementType = collectionType.ElementType().Definition.GetClrType( assemblies );
                var substitutedType = elementType.SubstituteIfNecessary( context );

                Type = typeof( IEnumerable<> ).MakeGenericType( substitutedType );
            }
            else
            {
                var parameterType = parameter.Type.Definition.GetClrType( assemblies );

                Type = parameterType.SubstituteIfNecessary( context );
            }

            Attributes = AttributesFromOperationParameter( parameter );
        }

        internal IEnumerable<CustomAttributeBuilder> Attributes { get; }

        public override int GetHashCode() => ( Name.GetHashCode() * 397 ) ^ Type.GetHashCode();

        static IEnumerable<CustomAttributeBuilder> AttributesFromProperty( PropertyInfo clrProperty )
        {
            Contract.Requires( clrProperty != null );
            Contract.Ensures( Contract.Result<IEnumerable<CustomAttributeBuilder>>() != null );

            foreach ( var attribute in clrProperty.CustomAttributes )
            {
                var ctor = attribute.Constructor;
                var ctorArgs = attribute.ConstructorArguments.Select( a => a.Value ).ToArray();
                var namedProperties = new List<PropertyInfo>( attribute.NamedArguments.Count );
                var propertyValues = new List<object>( attribute.NamedArguments.Count );
                var namedFields = new List<FieldInfo>( attribute.NamedArguments.Count );
                var fieldValues = new List<object>( attribute.NamedArguments.Count );

                foreach ( var argument in attribute.NamedArguments )
                {
                    if ( argument.IsField )
                    {
                        namedFields.Add( (FieldInfo) argument.MemberInfo );
                        fieldValues.Add( argument.TypedValue.Value );
                    }
                    else
                    {
                        namedProperties.Add( (PropertyInfo) argument.MemberInfo );
                        propertyValues.Add( argument.TypedValue.Value );
                    }
                }

                yield return new CustomAttributeBuilder(
                    ctor,
                    ctorArgs,
                    namedProperties.ToArray(),
                    propertyValues.ToArray(),
                    namedFields.ToArray(),
                    fieldValues.ToArray() );
            }
        }

        static IEnumerable<CustomAttributeBuilder> AttributesFromOperationParameter( IEdmOperationParameter parameter )
        {
            Contract.Requires( parameter != null );
            Contract.Ensures( Contract.Result<IEnumerable<CustomAttributeBuilder>>() != null );

            if ( parameter.Type.IsNullable )
            {
                yield break;
            }

            var ctor = typeof( RequiredAttribute ).GetConstructors().Where( c => c.GetParameters().Length == 0 ).Single();
#if WEBAPI
            var args = new object[0];
#else
            var args = Array.Empty<object>();
#endif

            yield return new CustomAttributeBuilder( ctor, args );
        }
    }
}