﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ConferenceDTO;
using FrontEnd.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace FrontEnd.Pages
{
    public class IndexModel : PageModel
    {
        protected readonly IApiClient _apiClient;
        public IEnumerable<IGrouping<DateTimeOffset?, SessionResponse>> Sessions { get; set; }
        public IEnumerable<(int Offset, DayOfWeek? DayofWeek)> DayOffsets { get; set; }
        public int CurrentDayOffset { get; set; }
        public bool IsAdmin { get; set; }
        [TempData]
        public string Message { get; set; }
        public bool ShowMessage => !string.IsNullOrEmpty(Message);
        public List<SessionResponse> UserSessions { get; set; }
        public ConferenceData ConferenceModel { get; private set; }

        public IndexModel(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }
        protected virtual Task<List<SessionResponse>> GetSessionsAsync()
        {
            return _apiClient.GetSessionsAsync();
        }

        public async Task<IActionResult> OnGetAsync(int day = 0)
        {
            CurrentDayOffset = day;

            if (User.Identity.IsAuthenticated)
            {
                UserSessions = await _apiClient.GetSessionsByAttendeeAsync(User.Identity.Name);
            }

            ConferenceModel = GetConferenceDataAsync(); //await GetConferenceDataAsync();

            if (CurrentDayOffset > 0 && !ConferenceModel.ContainsKey(CurrentDayOffset))
            {
                // Requested day is no longer valid, redirect to first day
                return RedirectToPage();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int sessionId, int day = 0)
        {
            await _apiClient.AddSessionToAttendeeAsync(User.Identity.Name, sessionId);

            if (day > 0)
            {
                return RedirectToPage(new { day });
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveAsync(int sessionId, int day = 0)
        {
            await _apiClient.RemoveSessionFromAttendeeAsync(User.Identity.Name, sessionId);

            if (day > 0)
            {
                return RedirectToPage(new { day });
            }

            return RedirectToPage();
        }

        //protected virtual Task<ConferenceData> GetConferenceDataAsync()
        //{
        //    return _cache.GetOrCreateAsync(CacheKeys.ConferenceData, async entry =>
        //    {
        //        entry.SetSlidingExpiration(TimeSpan.FromHours(1));

        //        var sessions = await _apiClient.GetSessionsAsync();
        //        return GenerateConferenceData(sessions);
        //    });
        //}

        protected virtual ConferenceData GetConferenceDataAsync()
        {
            var sessions = GetSessionsAsync();
            return GenerateConferenceData(sessions.Result);
        }

        public static ConferenceData GenerateConferenceData(List<SessionResponse> sessions)
        {
            var startDate = sessions.Min(s => s.StartTime?.Date);

            var dayOffsets = sessions.Select(s => s.StartTime?.Date)
                                     .Distinct()
                                     .OrderBy(d => d)
                                     .Select(day => (Offset: (int)Math.Floor((day.Value - startDate)?.TotalDays ?? 0),
                                                     day?.DayOfWeek))
                                     .ToList();

            var confData = new ConferenceData(dayOffsets.Count);

            foreach (var day in dayOffsets)
            {
                var filterDate = startDate?.AddDays(day.Offset);

                confData[day.Offset] = sessions.Where(s => s.StartTime?.Date == filterDate)
                                               .OrderBy(s => s.Track.Name)
                                               .GroupBy(s => s.StartTime)
                                               .OrderBy(g => g.Key);
            }

            confData.StartDate = startDate;
            confData.DayOffsets = dayOffsets;

            return confData;
        }

        public class ConferenceData : Dictionary<int, IEnumerable<IGrouping<DateTimeOffset?, SessionResponse>>>
        {
            public ConferenceData(int capacity) : base(capacity)
            {
            }

            public DateTimeOffset? StartDate { get; set; }

            public DateTimeOffset? EndDate { get; set; }

            public IEnumerable<(int Offset, DayOfWeek? DayofWeek)> DayOffsets { get; set; }
        }
    }

}
