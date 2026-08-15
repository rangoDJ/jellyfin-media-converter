(function () {
    var selectedItemId = null;

    function apiGet(path) {
        return ApiClient.getJSON(ApiClient.getUrl(path));
    }

    function apiPost(path, body) {
        return ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl(path),
            data: JSON.stringify(body || {}),
            contentType: 'application/json',
            dataType: 'json'
        });
    }

    function showError(context, error) {
        console.error('Media Converter: ' + context, error);
        var banner = document.getElementById('mediaConverterError');
        if (banner) {
            var detail = (error && (error.message || error.statusText)) || 'request failed';
            banner.textContent = context + ': ' + detail;
            banner.style.display = 'block';
        }
    }

    function clearError() {
        var banner = document.getElementById('mediaConverterError');
        if (banner) {
            banner.style.display = 'none';
        }
    }

    function openConvertDialog(itemId) {
        selectedItemId = itemId;
        document.getElementById('convertDialog').style.display = 'block';
    }

    function renderLibrary(items) {
        document.getElementById('episodesSection').style.display = 'none';

        var container = document.getElementById('libraryResults');
        container.innerHTML = '';

        if (!items.length) {
            var empty = document.createElement('div');
            empty.textContent = 'No matches.';
            container.appendChild(empty);
            return;
        }

        items.forEach(function (item) {
            var row = document.createElement('div');
            row.className = 'listItem';

            var name = document.createElement('span');
            name.textContent = item.Name + ' (' + item.Type + ')';
            row.appendChild(name);

            var button = document.createElement('button');
            button.setAttribute('is', 'emby-button');
            button.className = 'raised';

            if (item.Type === 'Series') {
                button.textContent = 'Browse episodes';
                button.addEventListener('click', function () {
                    browseSeries(item.Id, item.Name);
                });
            } else {
                button.textContent = 'Convert';
                button.addEventListener('click', function () {
                    openConvertDialog(item.Id);
                });
            }

            row.appendChild(button);
            container.appendChild(row);
        });
    }

    function browseSeries(seriesId, seriesName) {
        apiGet('MediaConverter/Series/' + seriesId + '/Episodes')
            .then(function (episodes) {
                clearError();
                renderEpisodes(seriesName, episodes);
            })
            .catch(function (error) {
                showError('Loading episodes', error);
            });
    }

    function renderEpisodes(seriesName, episodes) {
        document.getElementById('episodesSeriesName').textContent = '- ' + seriesName;

        var container = document.getElementById('episodeResults');
        container.innerHTML = '';

        if (!episodes.length) {
            var empty = document.createElement('div');
            empty.textContent = 'No episodes found.';
            container.appendChild(empty);
        } else {
            episodes.forEach(function (episode) {
                var row = document.createElement('div');
                row.className = 'listItem';

                var label = document.createElement('span');
                var seasonEpisode = (episode.SeasonNumber != null && episode.EpisodeNumber != null)
                    ? 'S' + episode.SeasonNumber + 'E' + episode.EpisodeNumber + ' - '
                    : '';
                label.textContent = seasonEpisode + episode.Name;
                row.appendChild(label);

                var button = document.createElement('button');
                button.setAttribute('is', 'emby-button');
                button.className = 'raised';
                button.textContent = 'Convert';
                button.addEventListener('click', function () {
                    openConvertDialog(episode.Id);
                });
                row.appendChild(button);

                container.appendChild(row);
            });
        }

        document.getElementById('episodesSection').style.display = 'block';
    }

    function renderJobs(jobs) {
        var container = document.getElementById('jobResults');
        container.innerHTML = '';

        jobs.forEach(function (job) {
            var row = document.createElement('div');
            row.className = 'listItem';

            var label = document.createElement('span');
            label.textContent = job.SourcePath + ' - ' + job.Status + ' (' + Math.round(job.ProgressPercent) + '%)';
            row.appendChild(label);

            if (job.Status === 'Queued' || job.Status === 'Running') {
                var cancelButton = document.createElement('button');
                cancelButton.setAttribute('is', 'emby-button');
                cancelButton.className = 'raised';
                cancelButton.textContent = 'Cancel';
                cancelButton.addEventListener('click', function () {
                    apiPost('MediaConverter/Jobs/' + job.Id + '/Cancel').catch(function (error) {
                        showError('Cancelling job', error);
                    });
                });
                row.appendChild(cancelButton);
            }

            container.appendChild(row);
        });
    }

    function refreshJobs() {
        apiGet('MediaConverter/Jobs')
            .then(renderJobs)
            .catch(function (error) {
                showError('Loading jobs', error);
            });
    }

    function search() {
        var searchTerm = document.getElementById('searchTerm').value || '';
        apiGet('MediaConverter/Library?searchTerm=' + encodeURIComponent(searchTerm))
            .then(function (items) {
                clearError();
                renderLibrary(items);
            })
            .catch(function (error) {
                showError('Searching library', error);
            });
    }

    function init() {
        document.getElementById('searchButton').addEventListener('click', search);

        document.getElementById('backToResultsButton').addEventListener('click', function () {
            document.getElementById('episodesSection').style.display = 'none';
        });

        document.getElementById('startConversionButton').addEventListener('click', function () {
            if (!selectedItemId) {
                return;
            }

            var audioBitrateValue = document.getElementById('audioBitrateInput').value;
            var resolutionValue = document.getElementById('resolutionSelect').value;

            apiPost('MediaConverter/Convert', {
                ItemId: selectedItemId,
                Container: document.getElementById('containerSelect').value,
                VideoCodec: document.getElementById('codecSelect').value,
                Quality: parseInt(document.getElementById('qualityInput').value, 10),
                Mode: document.getElementById('modeSelect').value,
                Preset: document.getElementById('presetSelect').value || null,
                ScaleHeight: resolutionValue ? parseInt(resolutionValue, 10) : null,
                AudioCodec: document.getElementById('audioCodecSelect').value,
                AudioBitrateKbps: audioBitrateValue ? parseInt(audioBitrateValue, 10) : null,
                SubtitleMode: document.getElementById('subtitleModeSelect').value,
                FfmpegArgsOverride: document.getElementById('ffmpegOverrideInput').value || null
            }).then(function () {
                clearError();
                document.getElementById('convertDialog').style.display = 'none';
                refreshJobs();
            }).catch(function (error) {
                showError('Starting conversion', error);
            });
        });

        refreshJobs();
        setInterval(refreshJobs, 3000);
    }

    init();

    // Belt-and-braces: some Jellyfin web client versions re-fire this custom event
    // on subsequent navigations to the (cached) page without re-running its scripts.
    document.addEventListener('pageshow', refreshJobs);
})();
