// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

// Many samples return a sensitive rental to the shared secure pool, then check that the memory they kept is zero. A
// sample running in parallel can rent that memory and write to it before the check runs, so the samples run one at
// a time (ADR-1032).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
