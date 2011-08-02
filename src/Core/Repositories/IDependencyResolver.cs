﻿namespace NuGet {
    public interface IDependencyResolver {
        IPackage ResolveDependency(PackageDependency dependency, IPackageConstraintProvider constraintProvider);
    }
}