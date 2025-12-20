import React, { useEffect } from 'react';
import { Redirect, useHistory, useParams } from 'react-router';
import NotFound from 'Components/NotFound';
import usePrevious from 'Helpers/Hooks/usePrevious';
import useSeries from 'Series/useSeries';
import translate from 'Utilities/String/translate';
import SeriesDetails from './SeriesDetails';

function SeriesDetailsPage() {
  const { data: allSeries } = useSeries();
  const { titleSlug } = useParams<{ titleSlug: string }>();
  const history = useHistory();

  let seriesIndex = allSeries.findIndex(
    (series) => series.titleSlug === titleSlug
  );

  // If exact match not found, try to find a series whose slug starts with
  // the requested slug followed by a dash (e.g., "pose" matches "pose-79084")
  // This handles TMDB-style slugs when accessed via TVDB-style URLs
  if (seriesIndex === -1) {
    const slugPrefix = `${titleSlug}-`;
    const matchingIndex = allSeries.findIndex((series) =>
      series.titleSlug.startsWith(slugPrefix)
    );

    if (matchingIndex !== -1) {
      // Redirect to the correct URL
      return (
        <Redirect
          to={`${window.Sonarr.urlBase}/series/${allSeries[matchingIndex].titleSlug}`}
        />
      );
    }
  }

  const previousIndex = usePrevious(seriesIndex);

  useEffect(() => {
    if (
      seriesIndex === -1 &&
      previousIndex !== -1 &&
      previousIndex !== undefined
    ) {
      history.push(`${window.Sonarr.urlBase}/`);
    }
  }, [seriesIndex, previousIndex, history]);

  if (seriesIndex === -1) {
    return <NotFound message={translate('SeriesCannotBeFound')} />;
  }

  return <SeriesDetails seriesId={allSeries[seriesIndex].id} />;
}

export default SeriesDetailsPage;
