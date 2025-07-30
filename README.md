# Survey-Creation-2

This is a survey creation app currently meant to create personal surveys to record and track data of your choice.

Once started, the application allows you to create any number of surveys. You can then choose an active survey on the home page, allowing you to answer that specific survey or to view data recorded from previous answers.

When creating a survey, a user has the choice of the 4 following types of questions: text question, number question, yes or no question, and a linear scale question (1 to 10, for example). Any number of questions can be added, and a title can be given to the survey to easily recognize it.

An example I used for myself is the creation of a "mood" survey which asks me questions about my day, starting with how I felt today on a scale of 1 to 10, allowing me to record data about what might affect my mood from day to day.

# Setup

### Backend

From the root folder:

    cd backend
    npm install
    npm run dev

### Database

MySQL needs to be installed, and proper credentials need to be included in a .env file.

From the root folder:

    cd backend
    npm run init-db

### Frontend

From the root folder:

    cd frontend
    npm install
    npm start

# Next steps

The option to share public surveys will be added in the future, so different users can answer a survey created by a single person.
