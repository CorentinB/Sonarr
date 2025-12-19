import React, { useCallback } from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import useExistingSeries from 'Series/useExistingSeries';
import ImportSeriesTitle from './ImportSeriesTitle';
import styles from './ImportSeriesSearchResult.css';

interface ImportSeriesSearchResultProps {
  tvdbId?: number;
  tmdbId?: number;
  title: string;
  year: number;
  network?: string;
  onPress: (tvdbId: number | undefined, tmdbId: number | undefined) => void;
}

function ImportSeriesSearchResult({
  tvdbId,
  tmdbId,
  title,
  year,
  network,
  onPress,
}: ImportSeriesSearchResultProps) {
  const isExistingSeries = useExistingSeries(tvdbId, tmdbId);

  const handlePress = useCallback(() => {
    onPress(tvdbId, tmdbId);
  }, [tvdbId, tmdbId, onPress]);

  return (
    <div className={styles.container}>
      <Link className={styles.series} onPress={handlePress}>
        <ImportSeriesTitle
          title={title}
          year={year}
          network={network}
          isExistingSeries={isExistingSeries}
        />
      </Link>

      {tvdbId ? (
        <Link
          className={styles.tvdbLink}
          to={`https://www.thetvdb.com/?tab=series&id=${tvdbId}`}
        >
          <Icon
            className={styles.tvdbLinkIcon}
            name={icons.EXTERNAL_LINK}
            size={16}
          />
        </Link>
      ) : null}
    </div>
  );
}

export default ImportSeriesSearchResult;
