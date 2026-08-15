(function () {
    var selectedItemIds = [];

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

    function formatBytes(bytes) {
        if (!bytes) {
            return '';
        }

        var gb = bytes / (1024 * 1024 * 1024);
        if (gb >= 1) {
            return gb.toFixed(2) + ' GB';
        }

        return (bytes / (1024 * 1024)).toFixed(0) + ' MB';
    }

    function formatMediaInfo(info) {
        if (!info) {
            return 'stats unavailable';
        }

        var parts = [];

        if (info.VideoCodec) {
            var resolution = (info.Width && info.Height) ? (' ' + info.Width + 'x' + info.Height) : '';
            parts.push(info.VideoCodec.toUpperCase() + resolution);
        }

        if (info.AudioCodec) {
            var channels = info.AudioChannels ? (' ' + info.AudioChannels + 'ch') : '';
            parts.push(info.AudioCodec.toUpperCase() + channels);
        }

        var size = formatBytes(info.FileSizeBytes);
        if (size) {
            parts.push(size);
        }

        return parts.length ? parts.join(' · ') : 'stats unavailable';
    }

    function loadMediaInfo(itemId, span) {
        span.textContent = 'Loading stats…';
        apiGet('MediaConverter/Items/' + itemId + '/MediaInfo')
            .then(function (info) {
                span.textContent = formatMediaInfo(info);
            })
            .catch(function () {
                span.textContent = '';
            });
    }

    function appendStatsSpan(row, itemId) {
        var stats = document.createElement('span');
        stats.className = 'mcStats';
        stats.style.marginLeft = '8px';
        stats.style.opacity = '0.7';
        row.appendChild(stats);
        loadMediaInfo(itemId, stats);
    }

    function openConvertDialog(itemIds, label) {
        selectedItemIds = itemIds;
        var title = itemIds.length > 1 ? 'Convert ' + itemIds.length + ' items' : 'Convert';
        document.getElementById('convertDialogTitle').textContent = label ? title + ' – ' + label : title;
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

            if (item.Type === 'Series') {
                var browseButton = document.createElement('button');
                browseButton.setAttribute('is', 'emby-button');
                browseButton.className = 'raised';
                browseButton.textContent = 'Browse episodes';
                browseButton.addEventListener('click', function () {
                    browseSeries(item.Id, item.Name);
                });
                row.appendChild(browseButton);

                var convertAllButton = document.createElement('button');
                convertAllButton.setAttribute('is', 'emby-button');
                convertAllButton.className = 'raised';
                convertAllButton.textContent = 'Convert all episodes';
                convertAllButton.addEventListener('click', function () {
                    apiGet('MediaConverter/Series/' + item.Id + '/Episodes')
                        .then(function (episodes) {
                            clearError();
                            if (!episodes.length) {
                                showError('Convert all episodes', { message: 'No episodes found.' });
                                return;
                            }

                            openConvertDialog(episodes.map(function (e) { return e.Id; }), item.Name + ' – all episodes (' + episodes.length + ')');
                        })
                        .catch(function (error) {
                            showError('Loading episodes', error);
                        });
                });
                row.appendChild(convertAllButton);
            } else {
                appendStatsSpan(row, item.Id);

                var button = document.createElement('button');
                button.setAttribute('is', 'emby-button');
                button.className = 'raised';
                button.textContent = 'Convert';
                button.addEventListener('click', function () {
                    openConvertDialog([item.Id], item.Name);
                });
                row.appendChild(button);
            }

            container.appendChild(row);
        });
    }

    function browseSeries(seriesId, seriesName) {
        apiGet('MediaConverter/Series/' + seriesId + '/Episodes')
            .then(function (episodes) {
                clearError();
                renderEpisodes(seriesId, seriesName, episodes);
            })
            .catch(function (error) {
                showError('Loading episodes', error);
            });
    }

    function renderEpisodes(seriesId, seriesName, episodes) {
        document.getElementById('episodesSeriesName').textContent = '- ' + seriesName;

        var container = document.getElementById('episodeResults');
        container.innerHTML = '';

        if (!episodes.length) {
            var empty = document.createElement('div');
            empty.textContent = 'No episodes found.';
            container.appendChild(empty);
            document.getElementById('episodesSection').style.display = 'block';
            return;
        }

        var convertAllRow = document.createElement('div');
        convertAllRow.className = 'listItem';
        var convertAllButton = document.createElement('button');
        convertAllButton.setAttribute('is', 'emby-button');
        convertAllButton.className = 'raised';
        convertAllButton.textContent = 'Convert all ' + episodes.length + ' episodes';
        convertAllButton.addEventListener('click', function () {
            openConvertDialog(episodes.map(function (e) { return e.Id; }), seriesName + ' – all episodes');
        });
        convertAllRow.appendChild(convertAllButton);
        container.appendChild(convertAllRow);

        var seasons = {};
        var seasonOrder = [];
        episodes.forEach(function (episode) {
            var seasonKey = episode.SeasonNumber != null ? episode.SeasonNumber : -1;
            if (!seasons[seasonKey]) {
                seasons[seasonKey] = [];
                seasonOrder.push(seasonKey);
            }

            seasons[seasonKey].push(episode);
        });

        seasonOrder.forEach(function (seasonKey) {
            var seasonEpisodes = seasons[seasonKey];

            var seasonHeader = document.createElement('div');
            seasonHeader.className = 'listItem';

            var seasonLabel = document.createElement('h3');
            seasonLabel.textContent = seasonKey === -1 ? 'No season' : 'Season ' + seasonKey;
            seasonHeader.appendChild(seasonLabel);

            var convertSeasonButton = document.createElement('button');
            convertSeasonButton.setAttribute('is', 'emby-button');
            convertSeasonButton.className = 'raised';
            convertSeasonButton.textContent = 'Convert this season';
            convertSeasonButton.addEventListener('click', function () {
                openConvertDialog(seasonEpisodes.map(function (e) { return e.Id; }), seriesName + ' – Season ' + seasonKey);
            });
            seasonHeader.appendChild(convertSeasonButton);
            container.appendChild(seasonHeader);

            seasonEpisodes.forEach(function (episode) {
                var row = document.createElement('div');
                row.className = 'listItem';

                var label = document.createElement('span');
                var seasonEpisode = (episode.SeasonNumber != null && episode.EpisodeNumber != null)
                    ? 'S' + episode.SeasonNumber + 'E' + episode.EpisodeNumber + ' - '
                    : '';
                label.textContent = seasonEpisode + episode.Name;
                row.appendChild(label);

                appendStatsSpan(row, episode.Id);

                var button = document.createElement('button');
                button.setAttribute('is', 'emby-button');
                button.className = 'raised';
                button.textContent = 'Convert';
                button.addEventListener('click', function () {
                    openConvertDialog([episode.Id], episode.Name);
                });
                row.appendChild(button);

                container.appendChild(row);
            });
        });

        document.getElementById('episodesSection').style.display = 'block';
    }

    function renderVariantComparison(container, job, compare) {
        container.innerHTML = '';

        var originalBlock = document.createElement('div');
        originalBlock.textContent = 'Original: ' + formatMediaInfo(compare.Original);
        container.appendChild(originalBlock);

        var variantBlock = document.createElement('div');
        variantBlock.textContent = 'New variant: ' + formatMediaInfo(compare.Variant);
        container.appendChild(variantBlock);

        var keepVariantButton = document.createElement('button');
        keepVariantButton.setAttribute('is', 'emby-button');
        keepVariantButton.className = 'raised';
        keepVariantButton.textContent = 'Keep new variant (delete original)';
        keepVariantButton.addEventListener('click', function () {
            if (!window.confirm('Delete the original file and keep the new variant?')) {
                return;
            }

            apiPost('MediaConverter/Jobs/' + job.Id + '/KeepVariant')
                .then(function () {
                    clearError();
                    refreshJobs();
                })
                .catch(function (error) {
                    showError('Keeping new variant', error);
                });
        });

        var keepOriginalButton = document.createElement('button');
        keepOriginalButton.setAttribute('is', 'emby-button');
        keepOriginalButton.className = 'raised';
        keepOriginalButton.textContent = 'Keep original (delete new variant)';
        keepOriginalButton.addEventListener('click', function () {
            if (!window.confirm('Delete the new variant and keep the original file?')) {
                return;
            }

            apiPost('MediaConverter/Jobs/' + job.Id + '/KeepOriginal')
                .then(function () {
                    clearError();
                    refreshJobs();
                })
                .catch(function (error) {
                    showError('Keeping original', error);
                });
        });

        container.appendChild(keepVariantButton);
        container.appendChild(keepOriginalButton);
    }

    function buildVariantReviewPanel(job) {
        var panel = document.createElement('div');
        panel.className = 'listItem';
        panel.style.paddingLeft = '16px';

        var compareButton = document.createElement('button');
        compareButton.setAttribute('is', 'emby-button');
        compareButton.className = 'raised';
        compareButton.textContent = 'Compare original vs. new variant';

        var resultsBox = document.createElement('div');
        resultsBox.style.marginTop = '8px';

        compareButton.addEventListener('click', function () {
            compareButton.disabled = true;
            apiGet('MediaConverter/Jobs/' + job.Id + '/Compare')
                .then(function (compare) {
                    clearError();
                    renderVariantComparison(resultsBox, job, compare);
                })
                .catch(function (error) {
                    showError('Comparing variant', error);
                })
                .then(function () {
                    compareButton.disabled = false;
                });
        });

        panel.appendChild(compareButton);
        panel.appendChild(resultsBox);
        return panel;
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

            if (job.Mode === 'Variant' && job.Status === 'Completed' && job.VariantResolution === 'PendingReview') {
                container.appendChild(buildVariantReviewPanel(job));
            }
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
            if (!selectedItemIds.length) {
                return;
            }

            var audioBitrateValue = document.getElementById('audioBitrateInput').value;
            var resolutionValue = document.getElementById('resolutionSelect').value;

            apiPost('MediaConverter/Convert/Batch', {
                ItemIds: selectedItemIds,
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
