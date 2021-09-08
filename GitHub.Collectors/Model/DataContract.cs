// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class DataContract
    {
        public const string TeamMemberInstanceRecordType = "GitHub.Teams.MemberInstance";
        public const string TeamInstanceRecordType = "GitHub.Orgs.TeamInstance";
        public const string TeamRepositoryInstanceRecordType = "GitHub.Teams.RepositoryInstance";
        public const string RepositoryInstanceRecordType = "GitHub.Orgs.RepoInstance";

        public const string PathInstanceRecordType = "GitHub.Repos.PathInstance";
        public const string ViewInstanceRecordType = "GitHub.Repos.ViewInstance";
        public const string CloneInstanceRecordType = "GitHub.Repos.CloneInstance";
        public const string ReferrerInstanceRecordType = "GitHub.Repos.ReferrerInstance";
        public const string MilestoneInstanceRecordType = "GitHub.Repos.MilestoneInstance";
        public const string CommitInstanceRecordType = "GitHub.Repos.CommitInstance";
        public const string CommentInstanceRecordType = "GitHub.Repos.CommentInstance";
        public const string PullRequestInstanceRecordType = "GitHub.Repos.PullInstance";
        public const string PullRequestReviewsRecordType = "GitHub.Repos.Pulls.ReviewsInstance";
        public const string PullRequestCommentInstanceRecordType = "GitHub.Repos.Pulls.CommentInstance";
        public const string IssueInstanceRecordType = "GitHub.Repos.IssueInstance";
        public const string IssueCommentInstanceRecordType = "GitHub.Repos.Issues.CommentInstance";

        public const string TeamMembersApiName = "GitHub.Teams.Members";
        public const string TeamsApiName = "GitHub.Orgs.Teams";
        public const string TeamRepositoriesApiName = "GitHub.Teams.Repos";
        public const string RepositoriesApiName = "GitHub.Orgs.Repos";

        public const string PathsApiName = "GitHub.Repos.Paths";
        public const string ViewsApiName = "GitHub.Repos.Views";
        public const string ClonesApiName = "GitHub.Repos.Clones";
        public const string ReferrersApiName = "GitHub.Repos.Referrers";
        public const string MilestonesApiName = "GitHub.Repos.Milestones";
        public const string CommitInstanceApiName = "GitHub.Repos.CommitInstance";
        public const string CommitsApiName = "GitHub.Repos.Commits";
        public const string CommentsApiName = "GitHub.Repos.Comments";
        public const string PullRequestsApiName = "GitHub.Repos.Pulls";
        public const string PullRequestReviewsApiName = "GitHub.Repos.Pulls.Reviews";
        public const string PullRequestCommentsApiName = "GitHub.Repos.Pulls.Comments";
        public const string IssuesApiName = "GitHub.Repos.Issues";
        public const string IssueCommentsApiName = "GitHub.Repos.Issues.Comments";
    }
}
