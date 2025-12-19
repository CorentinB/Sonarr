import React, { useCallback, useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import Form from 'Components/Form/Form';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import SettingsToolbar from 'Settings/SettingsToolbar';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import {
  fetchMetadataSourceSettings,
  saveMetadataSourceSettings,
  setMetadataSourceSettingsValue,
} from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import TheTvdb from './TheTvdb';
import Tmdb from './Tmdb';

const SECTION = 'metadataSource';

function MetadataSourceSettings() {
  const dispatch = useDispatch();

  const {
    isFetching,
    isPopulated,
    isSaving,
    error,
    settings,
    hasSettings,
    hasPendingChanges,
    validationErrors,
    validationWarnings,
  } = useSelector(createSettingsSectionSelector(SECTION));

  const handleInputChange = useCallback(
    (change: InputChanged) => {
      // @ts-expect-error - actions aren't typed
      dispatch(setMetadataSourceSettingsValue(change));
    },
    [dispatch]
  );

  const handleSavePress = useCallback(() => {
    dispatch(saveMetadataSourceSettings());
  }, [dispatch]);

  useEffect(() => {
    dispatch(fetchMetadataSourceSettings());

    return () => {
      dispatch(clearPendingChanges({ section: `settings.${SECTION}` }));
    };
  }, [dispatch]);

  return (
    <PageContent title={translate('MetadataSourceSettings')}>
      <SettingsToolbar
        hasPendingChanges={hasPendingChanges}
        isSaving={isSaving}
        onSavePress={handleSavePress}
      />

      <PageContentBody>
        {isFetching ? <LoadingIndicator /> : null}

        {!isFetching && error ? (
          <div>{translate('UnableToLoadMetadataSourceSettings')}</div>
        ) : null}

        {hasSettings && isPopulated && !error ? (
          <Form
            validationErrors={validationErrors}
            validationWarnings={validationWarnings}
          >
            <TheTvdb />

            <Tmdb
              settings={settings}
              onInputChange={handleInputChange}
            />
          </Form>
        ) : null}
      </PageContentBody>
    </PageContent>
  );
}

export default MetadataSourceSettings;
