todo
	separate phase (and data structures) for dependency analysis => meta, contractUsage + factories, generics

	explicit stack machine instead of recursion

	refactor: get rid of stupid EndResolveDependencies

	speed up factories with arguments: should be no reflection at runtime

	get rid of stupid AnalizeDependenciesOnly

	reduce memory traffix (remove LINQ, use List, etc)

	replace exceptions with FuncResult-s for Configuration api