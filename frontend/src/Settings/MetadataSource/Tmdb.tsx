import React from 'react';
import FieldSet from 'Components/FieldSet';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import { inputTypes, sizes } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './Tmdb.css';

interface TmdbSetting {
  value: string | boolean;
  errors?: { message: string }[];
  warnings?: { message: string }[];
}

interface TmdbProps {
  tmdbDefaultForNewShows: TmdbSetting;
  tmdbApiKey: TmdbSetting;
  onInputChange: (change: InputChanged) => void;
}

function Tmdb({
  tmdbDefaultForNewShows,
  tmdbApiKey,
  onInputChange,
}: TmdbProps) {
  return (
    <FieldSet legend={translate('TMDB')}>
      <div className={styles.container}>
        <img
          className={styles.image}
          src={`${window.Sonarr.urlBase}/Content/Images/tmdb.svg`}
          alt="TMDB"
        />

        <div className={styles.info}>
          <div className={styles.title}>{translate('TMDB')}</div>
          <InlineMarkdown
            data={translate('TmdbInfoText', {
              url: 'https://www.themoviedb.org/settings/api',
            })}
          />

          <div className={styles.fields}>
            <FormGroup>
              <FormLabel>{translate('TmdbApiKey')}</FormLabel>

              <FormInputGroup
                type={inputTypes.TEXT}
                name="tmdbApiKey"
                helpText={translate('TmdbApiKeyHelpText')}
                helpLink="https://www.themoviedb.org/settings/api"
                onChange={onInputChange}
                {...tmdbApiKey}
              />
            </FormGroup>

            <FormGroup size={sizes.MEDIUM}>
              <FormLabel>{translate('TmdbDefaultForNewShows')}</FormLabel>

              <FormInputGroup
                type={inputTypes.CHECK}
                name="tmdbDefaultForNewShows"
                helpText={translate('TmdbDefaultForNewShowsHelpText')}
                onChange={onInputChange}
                {...tmdbDefaultForNewShows}
              />
            </FormGroup>
          </div>
        </div>
      </div>
    </FieldSet>
  );
}

export default Tmdb;
