﻿namespace AutoMapper;
[EditorBrowsable(EditorBrowsableState.Never)]
public class ConstructorMap
{
    private bool? _canResolve;
    private readonly List<ConstructorParameterMap> _ctorParams = new();
    public ConstructorInfo Ctor { get; private set; }
    public IReadOnlyCollection<ConstructorParameterMap> CtorParams => _ctorParams;
    public void Reset(ConstructorInfo ctor)
    {
        Ctor = ctor;
        _ctorParams.Clear();
        _canResolve = null;
    }
    public bool CanResolve
    {
        get => _canResolve ??= ParametersCanResolve();
        set => _canResolve = value;
    }
    private bool ParametersCanResolve()
    {
        foreach (var param in _ctorParams)
        {
            if (!param.CanResolveValue)
            {
                return false;
            }
        }
        return true;
    }
    public ConstructorParameterMap this[string name]
    {
        get
        {
            foreach (var param in _ctorParams)
            {
                if (param.DestinationName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return param;
                }
            }
            return null;
        }
    }
    public void AddParameter(ParameterInfo parameter, IEnumerable<MemberInfo> sourceMembers, TypeMap typeMap) =>
        _ctorParams.Add(new(typeMap, parameter, sourceMembers.ToArray()));
    public bool ApplyIncludedMember(IncludedMember includedMember)
    {
        var includedMap = includedMember.TypeMap.ConstructorMap;
        if (CanResolve || includedMap?.Ctor != Ctor)
        {
            return false;
        }
        bool canResolve = false;
        var includedParams = includedMap._ctorParams;
        for(int index = 0; index < includedParams.Count; index++)
        {
            var includedParam = includedParams[index];
            if (!includedParam.CanResolveValue || _ctorParams[index].CanResolveValue)
            {
                continue;
            }
            canResolve = true;
            _canResolve = null;
            _ctorParams[index] = new(includedParam, includedMember);
        }
        return canResolve;
    }
    public void ApplyInheritedMap(TypeMap inheritedMap, TypeMap thisMap)
    {
        if (CanResolve)
        {
            return;
        }
        bool canResolve = true;
        for (int index = 0; index < _ctorParams.Count; index++)
        {
            var thisParam = _ctorParams[index];
            if (thisParam.CanResolveValue)
            {
                continue;
            }
            var inheritedParam = inheritedMap.ConstructorMap[thisParam.DestinationName];
            if (inheritedParam == null || !inheritedParam.CanResolveValue || thisParam.DestinationType != inheritedParam.DestinationType)
            {
                canResolve = false;
                continue;
            }
            _ctorParams[index] = new(thisMap, inheritedParam);
        }
        _canResolve = canResolve;
    }
}
[EditorBrowsable(EditorBrowsableState.Never)]
public class ConstructorParameterMap : MemberMap
{
    public ConstructorParameterMap(TypeMap typeMap, ParameterInfo parameter, MemberInfo[] sourceMembers) : base(typeMap)
    {
        Parameter = parameter;
        if (sourceMembers.Length > 0)
        {
            MapByConvention(sourceMembers);
        }
        else
        {
            SourceMembers = Array.Empty<MemberInfo>();
        }
    }
    public ConstructorParameterMap(ConstructorParameterMap parameterMap, IncludedMember includedMember) :
        this(includedMember.TypeMap, parameterMap.Parameter, parameterMap.SourceMembers) =>
        IncludedMember = includedMember.Chain(parameterMap.IncludedMember);
    public ConstructorParameterMap(TypeMap typeMap, ConstructorParameterMap inheritedParameterMap) : base(typeMap)
    {
        Parameter = inheritedParameterMap.Parameter;
        Resolver = inheritedParameterMap.Resolver;
    }
    public ParameterInfo Parameter { get; }
    public override Type DestinationType => Parameter.ParameterType;
    public override IncludedMember IncludedMember { get; }
    public override MemberInfo[] SourceMembers { get; set; }
    public override string DestinationName => Parameter.Name;
    public Expression DefaultValue(IGlobalConfiguration configuration) => Parameter.IsOptional ? Parameter.GetDefaultValue(configuration) : configuration.Default(DestinationType);
    public override string ToString() => $"{Constructor}, parameter {DestinationName}";
    private MemberInfo Constructor => Parameter.Member;
    public override bool? ExplicitExpansion { get; set; }
}