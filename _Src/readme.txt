todo
	separate phase (and data structures) for dependency analysis => meta, contractUsage + factories, generics

	explicit stack machine instead of recursion

	refactor: get rid of stupid EndResolveDependencies

	ContainerService: too heavy, split

	type, enumerable<constract> -> ContractsSet or something

	speed up factories: should be no reflection at runtime

	reuse configurators between local containers/static container ?

	run ServiceConfigurators on demand - when type is being resoved.
	This makes sense for optional services that requires non optional parameter
	from IParametersSource

	remove duplication with MatchWith/CanClose/GetClosingTypesSequence